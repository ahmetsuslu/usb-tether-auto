using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsbTetherAuto;

internal sealed class AppSettings
{
    /// <summary>Bos ise adb otomatik aranir.</summary>
    public string? AdbPath { get; set; }

    /// <summary>Telefon baglaninca tethering'i kendiliginden ac.</summary>
    public bool AutoEnable { get; set; } = true;

    /// <summary>Tepsi balonu bildirimleri.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Cihaz yoklama araligi (saniye).</summary>
    public int PollSeconds { get; set; } = 3;

    /// <summary>Son basarili yontem; sonraki denemede once bu kullanilir.</summary>
    public string? LastWorkingMethod { get; set; }

    [JsonIgnore]
    public int PollMilliseconds => Math.Clamp(PollSeconds, 1, 60) * 1000;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var text = File.ReadAllText(AppPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(text, Json);
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Ayarlar okunamadi, varsayilanlar kullanilacak: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDataDir();
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Ayarlar kaydedilemedi: {ex.Message}");
        }
    }
}
