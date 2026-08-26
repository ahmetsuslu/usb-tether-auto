using System.Diagnostics;
using System.Text;

namespace UsbTetherAuto;

internal sealed class MainForm : Form
{
    private const int MaxLogLines = 400;

    private readonly AppSettings _settings;
    private readonly AdbService _adb;
    private readonly TetherWatcher _watcher;

    private Label _dot = null!;
    private Label _headline = null!;
    private Label _detail = null!;
    private Button _btnEnable = null!;
    private Button _btnDisable = null!;
    private Button _btnRefresh = null!;
    private CheckBox _chkAuto = null!;
    private CheckBox _chkStartup = null!;
    private CheckBox _chkNotify = null!;
    private TextBox _txtAdb = null!;
    private Label _lblAdbResolved = null!;
    private TextBox _txtLog = null!;

    private bool _suppressSettingEvents;
    private bool _busy;
    private StatusEventArgs _lastStatus = new(TetherStatus.NoDevice, null, null);

    /// <summary>Tepsi menusunden "Cikis" secildiginde true yapilir.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AllowClose { get; set; }

    /// <summary>Penceredeki "otomatik ac" kutusu degistiginde tepsi menusu de guncellensin.</summary>
    public event EventHandler<bool>? AutoEnableChanged;

    /// <summary>Tepsi menusunden yapilan degisikligi pencereye yansitir (olay tetiklemez).</summary>
    public void SyncAutoCheckbox(bool value)
    {
        if (_chkAuto.Checked == value) return;
        _suppressSettingEvents = true;
        _chkAuto.Checked = value;
        _suppressSettingEvents = false;
    }

    public MainForm(AppSettings settings, AdbService adb, TetherWatcher watcher)
    {
        _settings = settings;
        _adb = adb;
        _watcher = watcher;

        BuildUi();
        LoadSettingsIntoUi();
        LoadExistingLog();

        Logger.Logged += OnLogged;
        _watcher.StatusChanged += OnStatusChanged;

        ApplyStatus(new StatusEventArgs(_watcher.Status, _watcher.DeviceModel, null));
    }

    // ---------------- arayuz kurulumu ----------------

    private void BuildUi()
    {
        Text = "USB Tether Auto";
        Icon = IconStore.App;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(660, 640);
        MinimumSize = new Size(560, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildStatusCard(), 0, 0);
        root.Controls.Add(BuildActions(), 0, 1);
        root.Controls.Add(BuildSettings(), 0, 2);
        root.Controls.Add(BuildLog(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);

        Controls.Add(root);
        Theme.ApplyTo(this);
    }

    private Control BuildStatusCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 0, 0, 10)
        };
        card.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
            Theme.Border, ButtonBorderStyle.Solid);

        _dot = new Label
        {
            Text = "●",
            Font = new Font("Segoe UI", 22F),
            AutoSize = true,
            Location = new Point(14, 22),
            ForeColor = Theme.Muted
        };

        _headline = new Label
        {
            Text = "Baslatiliyor...",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(58, 22),
            ForeColor = Theme.Text
        };

        _detail = new Label
        {
            Text = string.Empty,
            AutoSize = false,
            Location = new Point(60, 54),
            Size = new Size(540, 30),
            ForeColor = Theme.Muted,
            Tag = "muted"
        };

        card.Controls.AddRange([_dot, _headline, _detail]);
        return card;
    }

    private Control BuildActions()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 10),
            WrapContents = false
        };

        _btnEnable = MakeButton("Simdi ac", async (_, _) => await RunActionAsync(
            () => _watcher.EnableNowAsync(), "Tethering acildi.", "Tethering acilamadi."));

        _btnDisable = MakeButton("Kapat", async (_, _) => await RunActionAsync(
            () => _watcher.DisableNowAsync(), "Tethering kapatildi.", "Kapatilamadi."));

        _btnRefresh = MakeButton("Yenile", async (_, _) =>
        {
            SetBusy(true);
            try { await _watcher.RefreshAsync(); }
            finally { SetBusy(false); }
        });

        var guide = MakeButton("Kurulum rehberi", (_, _) => ShowSetupGuide());
        guide.Margin = new Padding(20, 0, 0, 0);

        row.Controls.AddRange([_btnEnable, _btnDisable, _btnRefresh, guide]);
        return row;
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(110, 32),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.System
        };
        b.Click += onClick;
        return b;
    }

    private Control BuildSettings()
    {
        var box = new GroupBox
        {
            Text = "Ayarlar",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 6, 10, 10),
            Margin = new Padding(0, 0, 0, 10),
            ForeColor = Theme.Text
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _chkAuto = MakeCheck("Telefon baglaninca tethering'i otomatik ac", OnAutoChanged);
        _chkStartup = MakeCheck("Windows ile birlikte baslat (tepside)", OnStartupChanged);
        _chkNotify = MakeCheck("Tepsi bildirimlerini goster", OnSimpleSettingChanged);

        grid.Controls.Add(_chkAuto, 0, 0);
        grid.SetColumnSpan(_chkAuto, 3);
        grid.Controls.Add(_chkStartup, 0, 1);
        grid.SetColumnSpan(_chkStartup, 3);
        grid.Controls.Add(_chkNotify, 0, 2);
        grid.SetColumnSpan(_chkNotify, 3);

        var lblAdb = new Label
        {
            Text = "adb.exe yolu:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 9, 6, 3)
        };
        _txtAdb = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Bos birakilirsa otomatik aranir",
            Margin = new Padding(0, 6, 6, 3)
        };
        _txtAdb.Leave += (_, _) => ApplyAdbPath();

        var browse = new Button
        {
            Text = "Gozat...",
            AutoSize = true,
            MinimumSize = new Size(84, 26),
            FlatStyle = FlatStyle.System,
            Margin = new Padding(0, 5, 0, 3)
        };
        browse.Click += (_, _) => BrowseForAdb();

        grid.Controls.Add(lblAdb, 0, 3);
        grid.Controls.Add(_txtAdb, 1, 3);
        grid.Controls.Add(browse, 2, 3);

        // AutoSize + ColumnSpan birlesimi TableLayoutPanel'de kirpilmaya yol aciyor;
        // bu yuzden sabit yukseklik + Dock kullaniliyor.
        _lblAdbResolved = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 24,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted,
            Tag = "muted",
            Margin = new Padding(3, 2, 3, 0)
        };
        grid.Controls.Add(_lblAdbResolved, 0, 4);
        grid.SetColumnSpan(_lblAdbResolved, 3);

        box.Controls.Add(grid);
        return box;
    }

    private static CheckBox MakeCheck(string text, EventHandler onChanged)
    {
        var check = new CheckBox
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(3, 4, 3, 4),
            Anchor = AnchorStyles.Left
        };
        check.CheckedChanged += onChanged;
        return check;
    }

    private Control BuildLog()
    {
        var box = new GroupBox
        {
            Text = "Kayitlar",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ForeColor = Theme.Text
        };

        _txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 8.75F),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };

        box.Controls.Add(_txtLog);
        return box;
    }

    private Control BuildFooter()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0),
            WrapContents = true
        };

        var openLog = new LinkLabel { Text = "Kayit dosyasini ac", AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
        openLog.LinkClicked += (_, _) => OpenPath(AppPaths.LogFile);

        var openDir = new LinkLabel { Text = "Veri klasorunu ac", AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
        openDir.LinkClicked += (_, _) => OpenPath(AppPaths.DataDir);

        var version = typeof(MainForm).Assembly.GetName().Version;
        var hint = new Label
        {
            Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}  -  Kapatinca tepside kalir.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Tag = "muted",
            Margin = new Padding(0, 4, 0, 0)
        };

        row.Controls.AddRange([openLog, openDir, hint]);
        return row;
    }

    // ---------------- ayar baglama ----------------

    private void LoadSettingsIntoUi()
    {
        _suppressSettingEvents = true;
        _chkAuto.Checked = _settings.AutoEnable;
        _chkNotify.Checked = _settings.ShowNotifications;
        _chkStartup.Checked = AutoStart.IsEnabled();
        _txtAdb.Text = _settings.AdbPath ?? string.Empty;
        _suppressSettingEvents = false;
        UpdateAdbLabel();
    }

    private void OnSimpleSettingChanged(object? sender, EventArgs e)
    {
        if (_suppressSettingEvents) return;
        _settings.ShowNotifications = _chkNotify.Checked;
        _settings.Save();
    }

    private void OnAutoChanged(object? sender, EventArgs e)
    {
        if (_suppressSettingEvents) return;
        _settings.AutoEnable = _chkAuto.Checked;
        _settings.Save();
        Logger.Info(_chkAuto.Checked ? "Otomatik acma acildi." : "Otomatik acma kapatildi.");
        AutoEnableChanged?.Invoke(this, _chkAuto.Checked);
    }

    private void OnStartupChanged(object? sender, EventArgs e)
    {
        if (_suppressSettingEvents) return;
        if (!AutoStart.Set(_chkStartup.Checked))
        {
            _suppressSettingEvents = true;
            _chkStartup.Checked = AutoStart.IsEnabled();
            _suppressSettingEvents = false;
        }
    }

    private void BrowseForAdb()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "adb.exe secin",
            Filter = "adb.exe|adb.exe|Tum dosyalar (*.*)|*.*",
            FileName = "adb.exe"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtAdb.Text = dialog.FileName;
            ApplyAdbPath();
        }
    }

    private async void ApplyAdbPath()
    {
        var value = _txtAdb.Text.Trim();
        var newPath = string.IsNullOrWhiteSpace(value) ? null : value;
        if (string.Equals(newPath, _settings.AdbPath, StringComparison.OrdinalIgnoreCase)) return;

        _settings.AdbPath = newPath;
        _settings.Save();
        _adb.InvalidatePath();
        UpdateAdbLabel();
        Logger.Info($"adb yolu ayarlandi: {_adb.AdbPath ?? "(bulunamadi)"}");

        try { await _watcher.RefreshAsync(); }
        catch (Exception ex) { Logger.Warn($"Yenileme basarisiz: {ex.Message}"); }
    }

    private void UpdateAdbLabel()
    {
        var resolved = _adb.AdbPath;
        _lblAdbResolved.Text = resolved is null
            ? "Kullanilan: bulunamadi - platform-tools kurun veya yolu elle secin"
            : $"Kullanilan: {resolved}";
        _lblAdbResolved.ForeColor = resolved is null ? IconStore.ColorForStatus(TetherStatus.Failed) : Theme.Muted;
    }

    // ---------------- durum ve kayit ----------------

    private void OnStatusChanged(object? sender, StatusEventArgs e) => RunOnUi(() => ApplyStatus(e));

    private void ApplyStatus(StatusEventArgs e)
    {
        _lastStatus = e;
        var color = IconStore.ColorForStatus(e.Status);
        _dot.ForeColor = color;
        _headline.Text = e.Headline;

        var parts = new List<string>();
        if (e.DeviceModel is not null) parts.Add(e.DeviceModel);
        if (e.Detail is not null) parts.Add(e.Detail);
        if (e.Status == TetherStatus.AdbMissing) parts.Add("adb.exe bulunamadi; ayarlardan yolunu secin.");
        if (e.Status == TetherStatus.Unauthorized) parts.Add("Telefondaki izin penceresini onaylayin.");
        _detail.Text = string.Join("  -  ", parts);

        var deviceReady = e.Status is TetherStatus.Connected or TetherStatus.Tethered or TetherStatus.Failed;
        _btnEnable.Enabled = !_busy && deviceReady && e.Status != TetherStatus.Tethered;
        _btnDisable.Enabled = !_busy && e.Status == TetherStatus.Tethered;
        _btnRefresh.Enabled = !_busy;
    }

    private void OnLogged(object? sender, LogEventArgs e) => RunOnUi(() => AppendLog(e.ToString()));

    private void LoadExistingLog()
    {
        try
        {
            if (!File.Exists(AppPaths.LogFile)) return;
            var lines = File.ReadAllLines(AppPaths.LogFile, Encoding.UTF8);
            var tail = lines.Length > MaxLogLines ? lines[^MaxLogLines..] : lines;
            _txtLog.Text = string.Join(Environment.NewLine, tail);
            ScrollLogToEnd();
        }
        catch
        {
            // kayit okunamazsa bos baslar
        }
    }

    private void AppendLog(string line)
    {
        if (_txtLog.Lines.Length > MaxLogLines)
            _txtLog.Lines = _txtLog.Lines[^(MaxLogLines / 2)..];

        _txtLog.AppendText(line + Environment.NewLine);
        ScrollLogToEnd();
    }

    private void ScrollLogToEnd()
    {
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    // ---------------- yardimcilar ----------------

    private async Task RunActionAsync(Func<Task<bool>> action, string okMessage, string failMessage)
    {
        SetBusy(true);
        try
        {
            var ok = await action();
            Logger.Info(ok ? okMessage : failMessage);
        }
        catch (Exception ex)
        {
            Logger.Error($"Islem hatasi: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Cursor = busy ? Cursors.AppStarting : Cursors.Default;
        // Dugme durumlari her zaman son bilinen duruma gore yeniden hesaplanir.
        ApplyStatus(_lastStatus);
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired) { try { BeginInvoke(action); } catch (ObjectDisposedException) { } }
        else action();
    }

    private static void OpenPath(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show($"Bulunamadi:\n{path}", "USB Tether Auto",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"Acilamadi ({path}): {ex.Message}");
        }
    }

    /// <summary>Telefonu hazirlama adimlarini anlatan pencereyi acar.</summary>
    public void ShowSetupGuide()
    {
        using var guide = new SetupGuideForm(_watcher, _adb);
        guide.ShowDialog(Visible ? this : null);
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        BringToFront();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Kullanici pencereyi kapatinca uygulama tepside calismaya devam eder.
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.ShowWindowMessage && m.Msg != 0)
        {
            ShowFromTray();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger.Logged -= OnLogged;
            _watcher.StatusChanged -= OnStatusChanged;
        }
        base.Dispose(disposing);
    }
}
