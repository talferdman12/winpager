namespace ChabadOfficePager;

/// <summary>Tiny append-only log. Best-effort: never throws, never blocks startup.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string LogPath =
        Path.Combine(Config.ConfigDirectory, "pager.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Config.ConfigDirectory);
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > 512 * 1024)
                    fi.Delete();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
