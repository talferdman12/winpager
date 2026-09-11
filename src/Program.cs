using WinPager.UI;

namespace WinPager;

public static class Program
{
    /// <summary>Guards against a second copy running and fighting over the UDP port.</summary>
    private const string SingleInstanceMutexName = @"Global\WinPager.SingleInstance";

    /// <summary>
    /// Passed to the copy launched by an update. It means "the previous version is
    /// still shutting down", so wait for it rather than reporting a clash.
    /// </summary>
    public const string RestartAfterUpdateFlag = "--updated";

    [STAThread]
    private static void Main()
    {
        var restartingAfterUpdate = Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, RestartAfterUpdateFlag, StringComparison.Ordinal));

        using var mutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName);

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(
                restartingAfterUpdate ? TimeSpan.FromSeconds(20) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // The previous run was killed rather than closed. The lock is ours now.
            acquired = true;
        }

        if (!acquired)
        {
            MessageBox.Show(
                "WinPager is already running. Look for the bell icon in your system tray.",
                "WinPager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Run();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static void Run()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Log.Write($"Unhandled UI exception: {e.Exception}");
            MessageBox.Show(
                $"Something went wrong:\n\n{e.Exception.Message}\n\nWinPager will keep running.",
                "WinPager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        // Safe to do now that we hold the lock: the build we replaced has exited, so
        // its file is no longer locked.
        UpdateChecker.CleanUpPreviousVersion();

        var config = Config.Load();

        // First run: make the user name their desk before anyone can page it "DESKTOP-4F9K2".
        var firstRunMarker = Path.Combine(Config.ConfigDirectory, ".configured");
        if (!File.Exists(firstRunMarker))
        {
            using var settings = new SettingsForm(config);
            if (settings.ShowDialog() != DialogResult.OK)
                return;

            Directory.CreateDirectory(Config.ConfigDirectory);
            File.WriteAllText(firstRunMarker, "1");
        }

        Application.Run(new TrayContext(config));
    }
}
