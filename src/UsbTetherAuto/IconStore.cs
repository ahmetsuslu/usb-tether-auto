using System.Reflection;

namespace UsbTetherAuto;

/// <summary>Gomulu .ico kaynaklarini yukler ve onbellekte tutar.</summary>
internal static class IconStore
{
    private static readonly Dictionary<string, Icon> Cache = new(StringComparer.Ordinal);
    private static readonly Assembly Owner = typeof(IconStore).Assembly;

    public static Icon App => Get("usb");

    public static Icon ForStatus(TetherStatus status) => status switch
    {
        TetherStatus.Tethered => Get("trayTethered"),
        TetherStatus.Connected or TetherStatus.Enabling => Get("trayConnected"),
        TetherStatus.NoCarrier => Get("trayWarn"),
        TetherStatus.Failed or TetherStatus.AdbMissing or TetherStatus.Unauthorized => Get("trayError"),
        _ => Get("trayOff")
    };

    public static Color ColorForStatus(TetherStatus status) => status switch
    {
        TetherStatus.Tethered => Color.FromArgb(0x16, 0xA3, 0x4A),
        TetherStatus.Connected or TetherStatus.Enabling => Color.FromArgb(0x25, 0x63, 0xEB),
        TetherStatus.NoCarrier => Color.FromArgb(0xD9, 0x77, 0x06),
        TetherStatus.Failed or TetherStatus.AdbMissing or TetherStatus.Unauthorized => Color.FromArgb(0xDC, 0x26, 0x26),
        _ => Color.FromArgb(0x6B, 0x72, 0x80)
    };

    public static Icon Get(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;

            var icon = Load(name) ?? SystemIcons.Application;
            Cache[name] = icon;
            return icon;
        }
    }

    private static Icon? Load(string name)
    {
        try
        {
            using var stream = Owner.GetManifestResourceStream($"UsbTetherAuto.Resources.{name}.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }
}
