using WinPager.UI;

namespace WinPager;

internal static class Program
{
    /// <summary>Guards against a second copy running and fighting over the UDP port.</summary>
    private const string SingleInstanceMutexName = @"Global\WinPager.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "WinPager is already running. Look for the bell icon in your system tray.",
                "WinPager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Log.Write($"Unhandled UI exception: {e.Exception}");
            MessageBox.Show(
                $"Something went wrong:\n\n{e.Exception.Message}\n\nThe pager will keep running.",
                "WinPager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        var config = Config.Load();

        // First run: make the user name their desk before anyone can page it "DESKTOP-4F9K2".
        if (!File.Exists(Path.Combine(Config.ConfigDirectory, ".configured")))
        {
            using var settings = new SettingsForm(config);
            if (settings.ShowDialog() != DialogResult.OK)
                return;

            File.WriteAllText(Path.Combine(Config.ConfigDirectory, ".configured"), "1");
        }

        Application.Run(new TrayContext(config));
    }
}
