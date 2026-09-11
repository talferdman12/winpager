using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace WinPager;

/// <summary>
/// Asks GitHub whether a newer release exists. It never installs anything by
/// itself; the most it does is offer to open the download page in a browser.
/// </summary>
public static class UpdateChecker
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/talferdman12/winpager/releases/latest";

    public const string ReleasesPage =
        "https://github.com/talferdman12/winpager/releases/latest";

    /// <summary>The version of the running executable, e.g. 1.0.1.
    /// Declared before <see cref="Http"/> on purpose: static fields initialise in
    /// declaration order, and building the client reads this value.</summary>
    public static Version CurrentVersion { get; } = ReadCurrentVersion();

    private static readonly HttpClient Http = CreateClient();

    public sealed record Result(
        bool Succeeded,
        Version? Latest,
        string? DownloadUrl,
        string? Error,
        string? AssetUrl = null)
    {
        public bool IsNewerThanInstalled =>
            Succeeded && Latest is not null && Latest > CurrentVersion;

        /// <summary>True when we found a build matching this PC's processor, so the
        /// update can be applied in place instead of sending the user to a web page.</summary>
        public bool CanInstallInPlace => IsNewerThanInstalled && !string.IsNullOrEmpty(AssetUrl);
    }

    private static Version ReadCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null
            ? new Version(0, 0, 0)
            : new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // GitHub rejects requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("WinPager", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    public static async Task<Result> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http
                .GetAsync(LatestReleaseApi, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Write($"Update check returned {(int)response.StatusCode}");
                return new Result(false, null, null, $"GitHub replied {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var page = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : ReleasesPage;
            var asset = FindAssetForThisMachine(doc.RootElement);

            if (!TryParseTag(tag, out var latest))
            {
                Log.Write($"Update check could not read the version tag '{tag}'");
                return new Result(false, null, null, "The latest release has an unreadable version tag.");
            }

            Log.Write($"Update check: installed {CurrentVersion}, latest {latest}, asset {asset ?? "none"}");
            return new Result(true, latest, page ?? ReleasesPage, null, asset);
        }
        catch (TaskCanceledException)
        {
            return new Result(false, null, null, "The check timed out.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            Log.Write($"Update check failed: {ex.Message}");
            return new Result(false, null, null, "Could not reach GitHub.");
        }
    }

    /// <summary>
    /// Picks the release file built for this PC's processor. Releases carry both an
    /// x64 and an arm64 build, and running the wrong one fails at launch.
    /// </summary>
    private static string? FindAssetForThisMachine(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;

        var wanted = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => null,
        };

        if (wanted is null) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null || !name.Contains(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

            return asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() : null;
        }

        return null;
    }

    /// <summary>
    /// Downloads the new build and swaps it in. Windows will not let a running
    /// program be overwritten, but it will let one be renamed, so the current file
    /// steps aside and the new one takes its place. On success the caller should
    /// start the returned path and exit.
    /// </summary>
    /// <returns>The path to launch, or null with <paramref name="error"/> set.</returns>
    public static Task<string?> DownloadAndSwapAsync(
        string assetUrl, CancellationToken cancellationToken = default)
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrEmpty(current))
        {
            Log.Write("Update aborted: could not determine the running executable's path.");
            return Task.FromResult<string?>(null);
        }

        return DownloadAndSwapAsync(assetUrl, current, cancellationToken);
    }

    /// <summary>
    /// The swap itself, against an explicit target path so it can be exercised
    /// without pointing at the running executable.
    /// </summary>
    public static async Task<string?> DownloadAndSwapAsync(
        string assetUrl, string current, CancellationToken cancellationToken = default)
    {
        var incoming = current + ".new";
        var retired = current + ".old";

        try
        {
            using (var response = await Http.GetAsync(
                       assetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                   .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using var file = File.Create(incoming);
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(incoming).Length < 32 * 1024)
                throw new IOException("The downloaded file is too small to be a build.");

            // Clear anything left by a previous update before reusing the name.
            if (File.Exists(retired)) File.Delete(retired);

            File.Move(current, retired);

            try
            {
                File.Move(incoming, current);
            }
            catch
            {
                // Put the working build back rather than leaving nothing behind.
                File.Move(retired, current);
                throw;
            }

            Log.Write($"Update installed over {current}");
            return current;
        }
        catch (Exception ex)
        {
            Log.Write($"Update install failed: {ex.Message}");
            TryDelete(incoming);
            return null;
        }
    }

    /// <summary>Removes the previous build left behind by an update.</summary>
    public static void CleanUpPreviousVersion()
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrEmpty(current)) return;

        TryDelete(current + ".old");
        TryDelete(current + ".new");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            // The old build is still locked, or we lack rights. Harmless either way.
            Log.Write($"Could not delete {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    /// <summary>Turns a release tag like "v1.0.2" into a comparable version.</summary>
    private static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var trimmed = tag.TrimStart('v', 'V').Trim();
        if (!Version.TryParse(trimmed, out var parsed)) return false;

        version = new Version(parsed.Major, parsed.Minor, parsed.Build < 0 ? 0 : parsed.Build);
        return true;
    }

    /// <summary>Open the download page in the user's browser.</summary>
    public static void OpenDownloadPage(string? url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url ?? ReleasesPage) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write($"Could not open the download page: {ex.Message}");
        }
    }
}
