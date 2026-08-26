using Microsoft.Win32;

namespace UsbTetherAuto;

/// <summary>Windows oturum acilisinda otomatik baslatma (HKCU\...\Run).</summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UsbTetherAuto";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string s && s.Contains("UsbTetherAuto", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return false;

            if (enabled)
            {
                // --tray: acilista pencere gosterme
                key.SetValue(ValueName, $"\"{AppPaths.ExecutablePath}\" --tray");
                Logger.Info("Windows ile otomatik baslatma acildi.");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Logger.Info("Windows ile otomatik baslatma kapatildi.");
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Otomatik baslatma ayarlanamadi: {ex.Message}");
            return false;
        }
    }
}
