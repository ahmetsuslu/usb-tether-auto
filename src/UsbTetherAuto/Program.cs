namespace UsbTetherAuto;

internal static class Program
{
    private const string MutexName = @"Local\UsbTetherAuto.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Zaten calisiyor: yeni ornek acmak yerine mevcut pencereyi one getir.
            NativeMethods.AskRunningInstanceToShow();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);

        // Elle calistirilinca pencere acilir; Windows acilisinda (--tray) dogrudan tepsiye iner.
        var startHidden = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));

        Application.Run(new TrayApplicationContext(startHidden));

        GC.KeepAlive(mutex);
    }

    private static void ReportCrash(Exception? ex)
    {
        if (ex is null) return;
        Logger.Error($"Beklenmeyen hata: {ex}");
        MessageBox.Show($"Beklenmeyen bir hata olustu:\n\n{ex.Message}\n\nAyrintilar kayit dosyasinda.",
            "USB Tether Auto", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
