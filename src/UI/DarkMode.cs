using System.Runtime.InteropServices;

namespace WinPager.UI;

/// <summary>Paints a window's title bar dark so it matches the app's own colours.</summary>
public static class DarkMode
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int valueSize);

    public static void Apply(IWin32Window window)
    {
        var enabled = 1;

        // The attribute id changed in Windows 10 20H1. Try the current one, then the
        // older one. Both fail harmlessly on versions that support neither.
        if (DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
    }
}
