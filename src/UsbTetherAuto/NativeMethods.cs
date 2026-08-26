using System.Runtime.InteropServices;

namespace UsbTetherAuto;

/// <summary>
/// Ikinci kez calistirildiginda yeni bir ornek acmak yerine mevcut pencereyi
/// one getirmek icin kullanilan Win32 cagrilari.
/// </summary>
internal static class NativeMethods
{
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);

    /// <summary>Sisteme kayitli ozel pencere mesajimiz.</summary>
    public static int ShowWindowMessage { get; } = RegisterWindowMessage("UsbTetherAuto.ShowMainWindow");

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Calisan diger ornege "pencereni goster" der.</summary>
    public static void AskRunningInstanceToShow()
    {
        if (ShowWindowMessage == 0) return;
        PostMessage(HwndBroadcast, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
    }
}
