using Microsoft.Win32;

namespace UsbTetherAuto;

/// <summary>Windows'un acik/koyu temasina uyan basit renk paleti.</summary>
internal static class Theme
{
    public static bool IsDark { get; } = DetectDark();

    public static Color Background => IsDark ? Color.FromArgb(0x1F, 0x1F, 0x1F) : Color.FromArgb(0xFA, 0xFA, 0xFA);
    public static Color Surface => IsDark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.White;
    public static Color Text => IsDark ? Color.FromArgb(0xE8, 0xE8, 0xE8) : Color.FromArgb(0x1A, 0x1A, 0x1A);
    public static Color Muted => IsDark ? Color.FromArgb(0x9A, 0x9A, 0x9A) : Color.FromArgb(0x66, 0x66, 0x66);
    public static Color Border => IsDark ? Color.FromArgb(0x3C, 0x3C, 0x3C) : Color.FromArgb(0xDD, 0xDD, 0xDD);
    public static Color Accent => Color.FromArgb(0x25, 0x63, 0xEB);

    private static bool DetectDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyTo(Control root)
    {
        root.BackColor = Background;
        root.ForeColor = Text;
        Walk(root);
    }

    private static void Walk(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case TextBox tb:
                    tb.BackColor = Surface;
                    tb.ForeColor = Text;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Button btn:
                    btn.FlatStyle = FlatStyle.System;
                    break;
                case GroupBox or Label or CheckBox or Panel or FlowLayoutPanel or TableLayoutPanel:
                    c.ForeColor = c is Label { Tag: "muted" } ? Muted : Text;
                    break;
            }
            Walk(c);
        }
    }
}
