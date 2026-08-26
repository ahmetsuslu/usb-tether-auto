using System.Diagnostics;

namespace UsbTetherAuto;

/// <summary>
/// Son kullanicilar "Gelistirici secenekleri"nin ne oldugunu bilmez. Bu pencere
/// telefonu hazirlama adimlarini sirayla anlatir ve canli durum gosterir:
/// adim tamamlandikca yesil isaret koyar, telefon gorununce "Bitti" der.
/// </summary>
internal sealed class SetupGuideForm : Form
{
    private const string PlatformToolsUrl = "https://developer.android.com/tools/releases/platform-tools";

    private readonly TetherWatcher _watcher;
    private readonly AdbService _adb;

    private readonly List<StepControl> _steps = [];
    private Label _liveStatus = null!;
    private Button _close = null!;

    public SetupGuideForm(TetherWatcher watcher, AdbService adb)
    {
        _watcher = watcher;
        _adb = adb;

        BuildUi();
        _watcher.StatusChanged += OnStatusChanged;
        ApplyStatus(new StatusEventArgs(_watcher.Status, _watcher.DeviceModel, null));
    }

    private void BuildUi()
    {
        Text = "Telefonu hazirlama";
        Icon = IconStore.App;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(700, 660);
        MinimumSize = new Size(620, 560);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18, 16, 18, 14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // baslik
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // adimlar
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // canli durum
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // dugmeler

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildSteps(), 0, 1);
        root.Controls.Add(BuildLiveStatus(), 0, 2);
        root.Controls.Add(BuildButtons(), 0, 3);

        Controls.Add(root);
        Theme.ApplyTo(this);
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "Telefonunuzu bir kez hazirlamaniz gerekiyor",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6)
        });

        panel.Controls.Add(new Label
        {
            Text = "Android, internet paylasimini disaridan acmaya yalnizca \"USB hata ayiklama\" "
                 + "acikken izin verir. Asagidaki adimlar bunu bir defaya mahsus yapar; "
                 + "telefonu yeniden baslatsaniz da ayar kalir.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Muted,
            Tag = "muted"
        });

        return panel;
    }

    private Control BuildSteps()
    {
        // Tek sutunlu TableLayoutPanel: adimlar pencere genisligine gore sarar,
        // yatay kaydirma cubugu cikmaz.
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoScroll = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddStep(host, StepKind.Adb,
            "Bilgisayarda \"adb\" araci",
            "Telefonla konusmak icin gereken kucuk bir Google araci. Genellikle zaten kuruludur; "
          + "degilse \"SDK Platform Tools\" zip'ini indirip C:\\platform-tools klasorune acmaniz yeterli.",
            "Indirme sayfasini ac", () => OpenUrl(PlatformToolsUrl));

        AddStep(host, StepKind.DeveloperOptions,
            "Telefonda \"Gelistirici secenekleri\"ni acin",
            "Ayarlar > Telefon hakkinda > Yazilim bilgileri yolunu izleyin ve \"Yapi numarasi\" "
          + "satirina arka arkaya 7 kez dokunun. Ekranda \"Artik bir gelistiricisiniz\" yazisi cikar. "
          + "(Ekran kilidiniz varsa PIN sorar.)");

        AddStep(host, StepKind.UsbDebugging,
            "\"USB hata ayiklama\"yi acin",
            "Ayarlar > Gelistirici secenekleri icine girin, listeden \"USB hata ayiklama\" "
          + "anahtarini acin ve cikan uyariyi onaylayin.");

        AddStep(host, StepKind.Authorize,
            "Kabloyu takin ve bilgisayara izin verin",
            "Telefonun kilidini acin. \"USB hata ayiklamaya izin verilsin mi?\" penceresi cikacak. "
          + "\"Bu bilgisayardan her zaman izin ver\" kutusunu isaretleyip Izin Ver deyin. "
          + "Bu kutuyu isaretlemezseniz her yeniden baslatmadan sonra tekrar sorar.");

        return host;
    }

    private void AddStep(TableLayoutPanel host, StepKind kind, string title, string body,
                         string? actionText = null, Action? action = null)
    {
        var step = new StepControl(_steps.Count + 1, kind, title, body, actionText, action)
        {
            Dock = DockStyle.Fill
        };
        _steps.Add(step);
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(step, 0, host.RowCount++);
    }

    private Control BuildLiveStatus()
    {
        _liveStatus = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 48,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = Theme.Surface,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };
        return _liveStatus;
    }

    private Control BuildButtons()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false
        };

        _close = new Button
        {
            Text = "Kapat",
            AutoSize = true,
            MinimumSize = new Size(110, 32),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.OK
        };

        row.Controls.Add(_close);
        CancelButton = _close;
        AcceptButton = _close;
        return row;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Adim listesi her zaman bastan gorunsun (odaklanan denetim listeyi kaydirmasin).
        _close.Focus();
        if (_steps.Count > 0 && _steps[0].Parent is ScrollableControl host)
            host.AutoScrollPosition = Point.Empty;
    }

    // ---------------- canli durum ----------------

    private void OnStatusChanged(object? sender, StatusEventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => ApplyStatus(e)); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
        else ApplyStatus(e);
    }

    private void ApplyStatus(StatusEventArgs e)
    {
        var adbOk = _adb.AdbPath is not null;
        // Cihaz adb'ye gorunuyorsa gelistirici secenekleri ve hata ayiklama zaten acilmistir.
        var debuggingOk = e.Status is not (TetherStatus.AdbMissing or TetherStatus.NoDevice);
        var authorized = debuggingOk && e.Status != TetherStatus.Unauthorized;

        foreach (var step in _steps)
        {
            step.SetDone(step.Kind switch
            {
                StepKind.Adb => adbOk,
                StepKind.DeveloperOptions or StepKind.UsbDebugging => debuggingOk,
                StepKind.Authorize => authorized,
                _ => false
            });
        }

        var (text, color) = e.Status switch
        {
            TetherStatus.AdbMissing =>
                ("adb bulunamadi - once 1. adimi tamamlayin.", IconStore.ColorForStatus(TetherStatus.Failed)),
            TetherStatus.NoDevice =>
                ("Telefon bekleniyor... Kablo takili ve USB hata ayiklama acik olmali.", Theme.Muted),
            TetherStatus.Unauthorized =>
                ("Telefon gorundu! Simdi ekrandaki izin penceresini onaylayin.",
                 IconStore.ColorForStatus(TetherStatus.Connected)),
            _ =>
                ($"Hazir - {e.DeviceModel ?? "telefon"} baglandi. Bu pencereyi kapatabilirsiniz.",
                 IconStore.ColorForStatus(TetherStatus.Tethered))
        };

        _liveStatus.Text = text;
        _liveStatus.ForeColor = color;
        _close.Text = authorized ? "Bitti" : "Kapat";
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Warn($"Baglanti acilamadi: {ex.Message}"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _watcher.StatusChanged -= OnStatusChanged;
        base.Dispose(disposing);
    }

    private enum StepKind { Adb, DeveloperOptions, UsbDebugging, Authorize }

    /// <summary>Numarali, tamamlaninca yesil isarete donen tek bir adim.</summary>
    private sealed class StepControl : TableLayoutPanel
    {
        private readonly Label _marker;
        private readonly string _number;

        public StepKind Kind { get; }

        public StepControl(int number, StepKind kind, string title, string body,
                           string? actionText, Action? action)
        {
            Kind = kind;
            _number = number.ToString();

            ColumnCount = 2;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(0, 0, 0, 14);
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _marker = new Label
            {
                Text = _number,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(30, 30),
                Margin = new Padding(0, 2, 12, 0),
                ForeColor = Color.White,
                BackColor = Theme.Muted
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 2)
            };

            // AutoSize + Dock.Fill, yuzde genislikli sutunda metni kendiliginden sarar.
            var bodyLabel = new Label
            {
                Text = body,
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                Tag = "muted",
                Margin = new Padding(0, 0, 0, 2)
            };

            Controls.Add(_marker, 0, 0);
            SetRowSpan(_marker, action is null ? 2 : 3);
            Controls.Add(titleLabel, 1, 0);
            Controls.Add(bodyLabel, 1, 1);

            if (actionText is not null && action is not null)
            {
                var link = new LinkLabel
                {
                    Text = actionText,
                    AutoSize = true,
                    Margin = new Padding(0, 4, 0, 0)
                };
                link.LinkClicked += (_, _) => action();
                Controls.Add(link, 1, 2);
            }
        }

        public void SetDone(bool done)
        {
            _marker.BackColor = done ? IconStore.ColorForStatus(TetherStatus.Tethered) : Theme.Muted;
            _marker.Text = done ? "\u2713" : _number;
        }
    }
}
