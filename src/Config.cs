using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChabadOfficePager;

/// <summary>
/// User settings, persisted to %APPDATA%\ChabadOfficePager\config.json.
/// </summary>
public sealed class Config
{
    /// <summary>Stable per-install identity. Generated once, never changes.</summary>
    public string DeviceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name shown to everyone else, e.g. "Front Desk" or "Rabbi's Office".</summary>
    public string DisplayName { get; set; } = Environment.MachineName;

    /// <summary>Play a sound when paged.</summary>
    public bool SoundOnPage { get; set; } = true;

    /// <summary>Show a full-screen-ish alert window in addition to the toast.</summary>
    public bool FlashWindowOnPage { get; set; } = true;

    /// <summary>Start automatically with Windows (writes an HKCU Run key).</summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>UDP port used for discovery and pages. Must match on every machine.</summary>
    public int Port { get; set; } = 45654;

    /// <summary>Shared secret; only instances with the same value talk to each other.</summary>
    public string GroupKey { get; set; } = "chabad-office";

    [JsonIgnore]
    public static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ChabadOfficePager");

    [JsonIgnore]
    public static string ConfigPath { get; } = Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Config>(json, JsonOptions);
                if (loaded is not null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.DeviceId))
                        loaded.DeviceId = Guid.NewGuid().ToString("N");
                    if (string.IsNullOrWhiteSpace(loaded.DisplayName))
                        loaded.DisplayName = Environment.MachineName;
                    if (loaded.Port is < 1024 or > 65535)
                        loaded.Port = 45654;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Config load failed: {ex.Message}");
        }

        var fresh = new Config();
        fresh.Save();
        return fresh;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"Config save failed: {ex.Message}");
        }
    }
}
