namespace UsbTetherAuto;

internal enum TetherStatus
{
    AdbMissing,
    NoDevice,
    Unauthorized,
    Connected,
    Enabling,
    Tethered,
    /// <summary>Arabirim ayakta ama tasiyici yok: paylasim acik gorunur, veri akmaz.</summary>
    NoCarrier,
    Failed
}

internal sealed class StatusEventArgs(TetherStatus status, string? deviceModel, string? detail) : EventArgs
{
    public TetherStatus Status { get; } = status;
    public string? DeviceModel { get; } = deviceModel;
    public string? Detail { get; } = detail;

    public string Headline => Status switch
    {
        TetherStatus.AdbMissing => "adb bulunamadi",
        TetherStatus.NoDevice => "Telefon bagli degil",
        TetherStatus.Unauthorized => "USB hata ayiklama izni bekleniyor",
        TetherStatus.Connected => "Bagli - tethering kapali",
        TetherStatus.Enabling => "Tethering aciliyor...",
        TetherStatus.Tethered => "Tethering acik",
        TetherStatus.NoCarrier => "Acik gorunuyor ama veri akmiyor",
        TetherStatus.Failed => "Tethering acilamadi",
        _ => "Bilinmiyor"
    };
}

internal sealed class NotifyEventArgs(string title, string text, ToolTipIcon icon) : EventArgs
{
    public string Title { get; } = title;
    public string Text { get; } = text;
    public ToolTipIcon Icon { get; } = icon;
}

/// <summary>
/// Cihazi surekli izler; baglandiginda (ve ayar acikken) tethering'i acar.
/// Tum adb erisimleri tek bir kapiden gecer, boylece elle verilen komutlarla
/// arka plan dongusu birbirine girmez.
/// </summary>
internal sealed class TetherWatcher(AdbService adb, AppSettings settings) : IDisposable
{
    private const int MaxConsecutiveFailures = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    private string? _serial;
    private string? _model;
    private int _failures;
    private DateTime _nextAttemptUtc = DateTime.MinValue;
    private TetherStatus _status = TetherStatus.NoDevice;

    public event EventHandler<StatusEventArgs>? StatusChanged;
    public event EventHandler<NotifyEventArgs>? Notification;

    public TetherStatus Status => _status;
    public string? DeviceModel => _model;

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        try { if (_loop is not null) await _loop.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* beklenen */ }
        _loop = null;
        _cts.Dispose();
        _cts = null;
    }

    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            await adb.StartServerAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Logger.Warn($"adb sunucusu baslatilamadi: {ex.Message}"); }

        while (!token.IsCancellationRequested)
        {
            try
            {
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try { await PollAsync(token).ConfigureAwait(false); }
                finally { _gate.Release(); }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Logger.Error($"Izleme dongusu hatasi: {ex.Message}");
            }

            try { await Task.Delay(settings.PollMilliseconds, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PollAsync(CancellationToken token)
    {
        var device = await adb.GetDeviceAsync(token).ConfigureAwait(false);

        if (device.State != DeviceState.Ready)
        {
            if (_serial is not null)
            {
                Logger.Info("Cihaz ayrildi.");
                Raise(Notification, new NotifyEventArgs("USB Tether Auto", "Telefon ayrildi.", ToolTipIcon.Info));
            }
            ResetDevice();
            SetStatus(device.State switch
            {
                DeviceState.AdbMissing => TetherStatus.AdbMissing,
                DeviceState.Unauthorized => TetherStatus.Unauthorized,
                _ => TetherStatus.NoDevice
            });
            return;
        }

        if (!string.Equals(_serial, device.Serial, StringComparison.Ordinal))
        {
            _serial = device.Serial;
            _failures = 0;
            _nextAttemptUtc = DateTime.MinValue;
            _model = await adb.GetModelAsync(_serial, token).ConfigureAwait(false);
            Logger.Info($"Cihaz baglandi: {_model ?? "(model okunamadi)"} [{_serial}]");
        }

        var link = await adb.GetLinkAsync(_serial, token).ConfigureAwait(false);

        if (link == TetherLink.Up)
        {
            var wasOff = _status != TetherStatus.Tethered;
            _failures = 0;
            SetStatus(TetherStatus.Tethered);
            if (wasOff)
                Raise(Notification, new NotifyEventArgs("USB Tether Auto",
                    $"{_model ?? "Telefon"} - internet paylasimi acik.", ToolTipIcon.Info));
            return;
        }

        if (link == TetherLink.NoCarrier)
        {
            // Arabirim duruyor ama USB tasiyicisi kalkmamis. setFunctions'i tekrar
            // denemek bu durumu duzeltmiyor; kullaniciya ne yapacagini soyluyoruz.
            var wasOk = _status != TetherStatus.NoCarrier;
            SetStatus(TetherStatus.NoCarrier,
                "Telefonu yeniden baslatin: paylasim arabirimi takili kalmis olabilir.");
            if (wasOk)
                Raise(Notification, new NotifyEventArgs("USB Tether Auto",
                    "Paylasim acik gorunuyor ama veri akmiyor. Telefonu yeniden baslatin.",
                    ToolTipIcon.Warning));
            return;
        }

        if (!settings.AutoEnable)
        {
            SetStatus(TetherStatus.Connected);
            return;
        }

        if (_failures >= MaxConsecutiveFailures)
        {
            SetStatus(TetherStatus.Failed, "Hicbir yontem ise yaramadi. Telefonu cikarip takin veya kayitlara bakin.");
            return;
        }

        if (DateTime.UtcNow < _nextAttemptUtc)
        {
            SetStatus(TetherStatus.Connected, "Yeniden denenecek...");
            return;
        }

        await TryEnableAsync(token).ConfigureAwait(false);
    }

    private async Task TryEnableAsync(CancellationToken token)
    {
        SetStatus(TetherStatus.Enabling);
        var method = await adb.EnableAsync(_serial, settings.LastWorkingMethod, token).ConfigureAwait(false);

        if (method is not null)
        {
            if (!string.Equals(settings.LastWorkingMethod, method, StringComparison.Ordinal))
            {
                settings.LastWorkingMethod = method;
                settings.Save();
            }
            _failures = 0;
            SetStatus(TetherStatus.Tethered, $"Yontem: {method}");
            Raise(Notification, new NotifyEventArgs("USB Tether Auto",
                $"{_model ?? "Telefon"} - internet paylasimi acildi.", ToolTipIcon.Info));
            return;
        }

        _failures++;
        _nextAttemptUtc = DateTime.UtcNow + RetryDelay;
        Logger.Error($"Tethering acilamadi ({_failures}/{MaxConsecutiveFailures}).");

        if (_failures >= MaxConsecutiveFailures)
        {
            SetStatus(TetherStatus.Failed, "Hicbir yontem ise yaramadi.");
            Raise(Notification, new NotifyEventArgs("USB Tether Auto",
                "Tethering acilamadi. Ayrintilar icin kayitlara bakin.", ToolTipIcon.Warning));
        }
        else
        {
            SetStatus(TetherStatus.Connected, "Yeniden denenecek...");
        }
    }

    // ---------------- elle verilen komutlar ----------------

    public async Task<bool> EnableNowAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var device = await adb.GetDeviceAsync(token).ConfigureAwait(false);
            if (device.State != DeviceState.Ready)
            {
                Logger.Warn("Elle acma istegi: cihaz hazir degil.");
                return false;
            }

            _serial = device.Serial;
            _failures = 0;
            _nextAttemptUtc = DateTime.MinValue;

            if (await adb.IsTetheredAsync(_serial, token).ConfigureAwait(false))
            {
                SetStatus(TetherStatus.Tethered);
                return true;
            }

            await TryEnableAsync(token).ConfigureAwait(false);
            return _status == TetherStatus.Tethered;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DisableNowAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var device = await adb.GetDeviceAsync(token).ConfigureAwait(false);
            if (device.State != DeviceState.Ready) return false;

            var ok = await adb.DisableAsync(device.Serial, token).ConfigureAwait(false);
            // Elle kapatildiysa otomatik acma hemen geri acmasin diye kisa bir bekleme.
            _nextAttemptUtc = DateTime.UtcNow + RetryDelay;
            SetStatus(ok ? TetherStatus.Connected : TetherStatus.Tethered);
            return ok;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Ayarlar degistiginde (ornegin adb yolu) durumu hemen tazeler.</summary>
    public async Task RefreshAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ResetDevice();
            await PollAsync(token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private void ResetDevice()
    {
        _serial = null;
        _model = null;
        _failures = 0;
        _nextAttemptUtc = DateTime.MinValue;
    }

    private void SetStatus(TetherStatus status, string? detail = null)
    {
        _status = status;
        Raise(StatusChanged, new StatusEventArgs(status, _model, detail));
    }

    private void Raise<T>(EventHandler<T>? handler, T args) where T : EventArgs
        => handler?.Invoke(this, args);

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _gate.Dispose();
    }
}
