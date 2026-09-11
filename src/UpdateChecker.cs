using System.Diagnostics;
using System.Net.Http;
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

    public sealed record Result(bool Succeeded, Version? Latest, string? DownloadUrl, string? Error)
    {
        public bool IsNewerThanInstalled =>
            Succeeded && Latest is not null && Latest > CurrentVersion;
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

            if (!TryParseTag(tag, out var latest))
            {
                Log.Write($"Update check could not read the version tag '{tag}'");
                return new Result(false, null, null, "The latest release has an unreadable version tag.");
            }

            Log.Write($"Update check: installed {CurrentVersion}, latest {latest}");
            return new Result(true, latest, page ?? ReleasesPage, null);
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
