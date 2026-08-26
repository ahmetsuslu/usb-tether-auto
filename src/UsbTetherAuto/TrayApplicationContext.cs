namespace UsbTetherAuto;

/// <summary>
/// Uygulamanin govdesi: tepsi simgesi, menusu ve arka plan izleyicisi.
/// Pencere kapatilsa bile bu baglam yasamaya devam eder.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly AdbService _adb;
    private readonly TetherWatcher _watcher;
    private readonly NotifyIcon _tray;
    private readonly MainForm _form;

    private readonly ToolStripMenuItem _miStatus;
    private readonly ToolStripMenuItem _miEnable;
    private readonly ToolStripMenuItem _miDisable;
    private readonly ToolStripMenuItem _miAuto;

    private bool _shuttingDown;

    public TrayApplicationContext(bool startHidden)
    {
        AppPaths.EnsureDataDir();

        // Ayar dosyasi yoksa bu ilk calistirma demektir; rehberi kendiliginden gosterelim.
        var firstRun = !File.Exists(AppPaths.SettingsFile);

        _settings = AppSettings.Load();
        _adb = new AdbService(() => _settings.AdbPath);
        _watcher = new TetherWatcher(_adb, _settings);

        _miStatus = new ToolStripMenuItem("Durum: baslatiliyor...") { Enabled = false };
        _miEnable = new ToolStripMenuItem("Tethering'i simdi ac", null, async (_, _) => await EnableAsync());
        _miDisable = new ToolStripMenuItem("Tethering'i kapat", null, async (_, _) => await DisableAsync());
        _miAuto = new ToolStripMenuItem("Otomatik ac", null, ToggleAuto) { CheckOnClick = true, Checked = _settings.AutoEnable };

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            _miStatus,
            new ToolStripSeparator(),
            _miEnable,
            _miDisable,
            _miAuto,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Pencereyi ac", null, (_, _) => ShowWindow()),
            new ToolStripMenuItem("Kayitlari ac", null, (_, _) => OpenLog()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Cikis", null, (_, _) => ExitApplication())
        ]);

        _tray = new NotifyIcon
        {
            Icon = IconStore.ForStatus(TetherStatus.NoDevice),
            Text = "USB Tether Auto",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        _form = new MainForm(_settings, _adb, _watcher);
        // Tek ornek mesajini alabilmesi icin pencere tutamaci simdi olusturulmali.
        _ = _form.Handle;

        _form.AutoEnableChanged += (_, value) => _miAuto.Checked = value;

        _watcher.StatusChanged += OnStatusChanged;
        _watcher.Notification += OnNotification;

        Logger.Info($"USB Tether Auto basladi. adb: {_adb.AdbPath ?? "(bulunamadi)"}");
        _watcher.Start();

        if (!startHidden)
        {
            _form.ShowFromTray();
            if (firstRun) _form.BeginInvoke(() => _form.ShowSetupGuide());
        }
    }

    // ---------------- tepsi olaylari ----------------

    private void OnStatusChanged(object? sender, StatusEventArgs e)
    {
        void Apply()
        {
            _tray.Icon = IconStore.ForStatus(e.Status);

            var tip = e.DeviceModel is null ? e.Headline : $"{e.Headline}\n{e.DeviceModel}";
            // NotifyIcon.Text 63 karakterle sinirli.
            _tray.Text = tip.Length > 62 ? tip[..62] : tip;

            _miStatus.Text = $"Durum: {e.Headline}";
            _miEnable.Enabled = e.Status is TetherStatus.Connected or TetherStatus.Failed;
            _miDisable.Enabled = e.Status == TetherStatus.Tethered;
        }

        RunOnUi(Apply);
    }

    private void OnNotification(object? sender, NotifyEventArgs e)
    {
        if (!_settings.ShowNotifications) return;
        RunOnUi(() => _tray.ShowBalloonTip(4000, e.Title, e.Text, e.Icon));
    }

    private void ToggleAuto(object? sender, EventArgs e)
    {
        _settings.AutoEnable = _miAuto.Checked;
        _settings.Save();
        Logger.Info(_settings.AutoEnable ? "Otomatik acma acildi." : "Otomatik acma kapatildi.");
        // Pencere aciksa oradaki kutu da guncellensin.
        RunOnUi(() => _form.SyncAutoCheckbox(_settings.AutoEnable));
    }

    private async Task EnableAsync()
    {
        _miEnable.Enabled = false;
        try { await _watcher.EnableNowAsync(); }
        catch (Exception ex) { Logger.Error($"Acma hatasi: {ex.Message}"); }
    }

    private async Task DisableAsync()
    {
        _miDisable.Enabled = false;
        try { await _watcher.DisableNowAsync(); }
        catch (Exception ex) { Logger.Error($"Kapatma hatasi: {ex.Message}"); }
    }

    private void ShowWindow() => _form.ShowFromTray();

    private static void OpenLog()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.LogFile)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"Kayit dosyasi acilamadi: {ex.Message}");
        }
    }

    private async void ExitApplication()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;

        Logger.Info("Uygulama kapatiliyor.");
        _tray.Visible = false;

        try { await _watcher.StopAsync(); }
        catch (Exception ex) { Logger.Warn($"Izleyici durdurulamadi: {ex.Message}"); }

        _form.AllowClose = true;
        _form.Close();
        ExitThread();
    }

    private void RunOnUi(Action action)
    {
        if (_form.IsDisposed) return;
        if (_form.InvokeRequired)
        {
            try { _form.BeginInvoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
        else action();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher.StatusChanged -= OnStatusChanged;
            _watcher.Notification -= OnNotification;
            _watcher.Dispose();
            _tray.Dispose();
            _form.Dispose();
        }
        base.Dispose(disposing);
    }
}
