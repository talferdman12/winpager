using Microsoft.Win32;

namespace ChabadOfficePager;

/// <summary>Adds or removes the HKCU Run entry that launches the pager at login.</summary>
public static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ChabadOfficePager";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            // A locked-down machine may block this; it isn't worth interrupting the user.
            Log.Write($"Startup registration failed: {ex.Message}");
        }
    }
}
