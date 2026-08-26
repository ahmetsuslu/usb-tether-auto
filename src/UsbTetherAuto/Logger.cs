using System.Text;

namespace UsbTetherAuto;

internal enum LogLevel { Info, Warn, Error }

internal sealed class LogEventArgs(DateTime time, LogLevel level, string message) : EventArgs
{
    public DateTime Time { get; } = time;
    public LogLevel Level { get; } = level;
    public string Message { get; } = message;

    public override string ToString() =>
        $"{Time:HH:mm:ss} [{Level.ToString().ToUpperInvariant()}] {Message}";
}

/// <summary>Diske ve arayuze ayni anda yazan basit kayit tutucu.</summary>
internal static class Logger
{
    private const long MaxBytes = 1024 * 1024;      // 1 MB
    private const int KeepLines = 500;              // kirpma sonrasi kalan satir

    private static readonly Lock Gate = new();

    public static event EventHandler<LogEventArgs>? Logged;

    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        var args = new LogEventArgs(DateTime.Now, level, message);
        Logged?.Invoke(null, args);

        try
        {
            lock (Gate)
            {
                AppPaths.EnsureDataDir();
                var line = $"{args.Time:yyyy-MM-dd HH:mm:ss} [{level.ToString().ToUpperInvariant()}] {message}";
                File.AppendAllText(AppPaths.LogFile, line + Environment.NewLine, Encoding.UTF8);
                TrimIfNeeded();
            }
        }
        catch
        {
            // Kayit tutmak asla uygulamayi dusurmemeli.
        }
    }

    private static void TrimIfNeeded()
    {
        var info = new FileInfo(AppPaths.LogFile);
        if (!info.Exists || info.Length <= MaxBytes) return;

        var lines = File.ReadAllLines(AppPaths.LogFile, Encoding.UTF8);
        if (lines.Length <= KeepLines) return;
        File.WriteAllLines(AppPaths.LogFile, lines[^KeepLines..], Encoding.UTF8);
    }
}
