using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace UsbTetherAuto;

internal enum DeviceState
{
    /// <summary>adb.exe bulunamadi.</summary>
    AdbMissing,
    /// <summary>Cihaz bagli degil.</summary>
    NoDevice,
    /// <summary>Cihaz bagli ama "USB hata ayiklama" izni verilmemis.</summary>
    Unauthorized,
    /// <summary>Cihaz hazir.</summary>
    Ready
}

internal sealed record DeviceInfo(DeviceState State, string? Serial);

/// <summary>Tethering'i acmak icin denenecek bir yontem.</summary>
internal sealed record TetherMethod(string Name, string Description, string[] Args);

internal sealed partial class AdbService(Func<string?> adbPathProvider)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Sirayla denenir; ilk ise yarayan ayarlarda hatirlanir.</summary>
    public static readonly TetherMethod[] Methods =
    [
        new("rndis", "USB fonksiyonunu RNDIS yap (Samsung One UI)",
            ["shell", "svc", "usb", "setFunctions", "rndis"]),
        new("ncm", "USB fonksiyonunu NCM yap (Android 11+)",
            ["shell", "svc", "usb", "setFunctions", "ncm"]),
        new("root-rndis", "Root ile RNDIS (yalnizca root'lu cihaz)",
            ["shell", "su", "-c", "svc usb setFunctions rndis"])
    ];

    private string? _resolvedPath;

    [GeneratedRegex(@"mCurrentFunctions\s*[:=]\s*([^\r\n]*)", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentFunctionsRegex();

    [GeneratedRegex(@"\b(rndis0|ncm0|usb0)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TetherInterfaceRegex();

    // ---------------- adb.exe bulma ----------------

    public string? AdbPath => _resolvedPath ??= ResolveAdbPath(adbPathProvider());

    /// <summary>Ayarlardaki yol degistiginde yeniden aramaya zorlar.</summary>
    public void InvalidatePath() => _resolvedPath = null;

    public static string? ResolveAdbPath(string? hint)
    {
        foreach (var candidate in EnumerateCandidates(hint))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }
        return null;
    }

    private static IEnumerable<string?> EnumerateCandidates(string? hint)
    {
        if (!string.IsNullOrWhiteSpace(hint)) yield return hint;

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, @"Android\Sdk\platform-tools\adb.exe");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(programFiles, @"Android\platform-tools\adb.exe");
        yield return @"C:\platform-tools\adb.exe";

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string combined;
            try { combined = Path.Combine(dir.Trim(), "adb.exe"); }
            catch { continue; }
            yield return combined;
        }
    }

    // ---------------- surec calistirma ----------------

    /// <summary>adb'yi calistirir; stdout+stderr'i tek metin olarak dondurur.</summary>
    public async Task<string> RunAsync(IEnumerable<string> args, CancellationToken token,
                                       TimeSpan? timeout = null)
    {
        var exe = AdbPath;
        if (exe is null) return "error: adb-not-found";

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();

        try
        {
            process.Start();

            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeout ?? DefaultTimeout);

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                TryKill(process);
                return "error: timeout";
            }

            output.Append(await stdout.ConfigureAwait(false));
            output.Append(await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }

        return output.ToString();
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* zaten sonlanmis olabilir */ }
    }

    private Task<string> ShellAsync(string? serial, CancellationToken token, params string[] args)
    {
        var full = new List<string>();
        if (serial is not null) { full.Add("-s"); full.Add(serial); }
        full.AddRange(args);
        return RunAsync(full, token);
    }

    // ---------------- durum sorgulari ----------------

    public Task StartServerAsync(CancellationToken token) =>
        RunAsync(["start-server"], token, TimeSpan.FromSeconds(20));

    public async Task<DeviceInfo> GetDeviceAsync(CancellationToken token)
    {
        if (AdbPath is null) return new DeviceInfo(DeviceState.AdbMissing, null);

        var output = await RunAsync(["devices"], token, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        string? unauthorized = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith('*') ||
                line.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("adb:", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            // Kablosuz adb (ip:port) degil, USB ile bagli cihaz istiyoruz.
            if (parts[0].Contains(':')) continue;

            if (parts[1].Equals("device", StringComparison.Ordinal))
                return new DeviceInfo(DeviceState.Ready, parts[0]);
            if (parts[1].Equals("unauthorized", StringComparison.Ordinal))
                unauthorized = parts[0];
        }

        return unauthorized is not null
            ? new DeviceInfo(DeviceState.Unauthorized, unauthorized)
            : new DeviceInfo(DeviceState.NoDevice, null);
    }

    public async Task<string?> GetModelAsync(string? serial, CancellationToken token)
    {
        var model = (await ShellAsync(serial, token, "shell", "getprop", "ro.product.model")
                     .ConfigureAwait(false)).Trim();
        return string.IsNullOrWhiteSpace(model) ||
               model.StartsWith("error", StringComparison.OrdinalIgnoreCase)
            ? null
            : model;
    }

    /// <summary>Tethering acik mi? Iki bagimsiz sinyale bakilir.</summary>
    public async Task<bool> IsTetheredAsync(string? serial, CancellationToken token)
    {
        var usb = await ShellAsync(serial, token, "shell", "dumpsys", "usb").ConfigureAwait(false);
        var match = CurrentFunctionsRegex().Match(usb);
        if (match.Success)
        {
            var functions = match.Groups[1].Value;
            if (functions.Contains("rndis", StringComparison.OrdinalIgnoreCase) ||
                functions.Contains("ncm", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var links = await ShellAsync(serial, token, "shell", "ip", "-o", "link").ConfigureAwait(false);
        return TetherInterfaceRegex().IsMatch(links);
    }

    // ---------------- acma / kapama ----------------

    /// <summary>
    /// Tethering'i acmayi dener. Basarili olan yontemin adini, hicbiri olmadiysa null doner.
    /// <paramref name="preferred"/> verilirse once o denenir.
    /// </summary>
    public async Task<string?> EnableAsync(string? serial, string? preferred, CancellationToken token)
    {
        foreach (var method in OrderMethods(preferred))
        {
            token.ThrowIfCancellationRequested();
            Logger.Info($"Yontem deneniyor: {method.Name}");

            var result = await ShellAsync(serial, token, method.Args).ConfigureAwait(false);
            if (LooksRejected(result))
            {
                Logger.Warn($"  {method.Name} reddedildi: {FirstLine(result)}");
                continue;
            }

            // setFunctions USB konfigurasyonunu degistirir; adb kisa sureligine duser.
            await Task.Delay(2500, token).ConfigureAwait(false);
            var device = await WaitForDeviceAsync(TimeSpan.FromSeconds(20), token).ConfigureAwait(false);
            if (device.State != DeviceState.Ready)
            {
                // adb geri gelmedi: USB fonksiyonu degismis ama adb kapanmis olabilir.
                Logger.Warn($"  {method.Name}: adb geri donmedi, sonuc dogrulanamadi.");
                return method.Name;
            }

            if (await IsTetheredAsync(device.Serial, token).ConfigureAwait(false))
            {
                Logger.Info($"  {method.Name} basarili.");
                return method.Name;
            }

            Logger.Warn($"  {method.Name} etkisiz.");
        }

        return null;
    }

    public async Task<bool> DisableAsync(string? serial, CancellationToken token)
    {
        Logger.Info("Tethering kapatiliyor.");
        var result = await ShellAsync(serial, token, "shell", "svc", "usb", "setFunctions").ConfigureAwait(false);
        if (LooksRejected(result))
        {
            Logger.Warn($"Kapatma reddedildi: {FirstLine(result)}");
            return false;
        }

        await Task.Delay(2000, token).ConfigureAwait(false);
        var device = await WaitForDeviceAsync(TimeSpan.FromSeconds(20), token).ConfigureAwait(false);
        if (device.State != DeviceState.Ready) return true;

        return !await IsTetheredAsync(device.Serial, token).ConfigureAwait(false);
    }

    private async Task<DeviceInfo> WaitForDeviceAsync(TimeSpan timeout, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + timeout;
        DeviceInfo info;
        do
        {
            info = await GetDeviceAsync(token).ConfigureAwait(false);
            if (info.State == DeviceState.Ready) return info;
            await Task.Delay(1000, token).ConfigureAwait(false);
        }
        while (DateTime.UtcNow < deadline);
        return info;
    }

    private static IEnumerable<TetherMethod> OrderMethods(string? preferred)
    {
        var first = Methods.FirstOrDefault(m => m.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase));
        if (first is not null) yield return first;
        foreach (var m in Methods)
            if (!ReferenceEquals(m, first)) yield return m;
    }

    private static bool LooksRejected(string output) =>
        output.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("SecurityException", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("inaccessible", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("adb-not-found", StringComparison.OrdinalIgnoreCase) ||
        output.Contains("error: timeout", StringComparison.OrdinalIgnoreCase);

    private static string FirstLine(string text)
    {
        var line = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "(bos yanit)";
        return line.Length > 160 ? line[..160] + "..." : line;
    }
}
