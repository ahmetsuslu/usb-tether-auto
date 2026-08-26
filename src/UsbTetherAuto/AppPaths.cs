namespace UsbTetherAuto;

/// <summary>Uygulamanin kullandigi dosya yollari.</summary>
internal static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UsbTetherAuto");

    public static string LogFile => Path.Combine(DataDir, "tether.log");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");

    // Tek dosyalik yayinda Assembly.Location bos doner; ProcessPath dogru olandir.
    public static string ExecutablePath => Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "UsbTetherAuto.exe");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
