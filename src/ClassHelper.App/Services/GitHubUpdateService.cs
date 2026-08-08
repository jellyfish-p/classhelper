using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.Services;

public sealed record UpdateCheckResult(
    SemanticVersion Version,
    string Tag,
    string ReleaseUrl,
    UpdateAsset Asset);

public sealed class GitHubUpdateService : IDisposable
{
    private const string ReleasesEndpoint = "https://api.github.com/repos/jellyfish-p/classhelper/releases?per_page=30";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GitHubUpdateService(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClassHelper", AppBuildInfo.DisplayVersion));
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult?> CheckAsync(
        UpdateChannel channel,
        CancellationToken cancellationToken)
    {
        var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(
            ReleasesEndpoint,
            cancellationToken) ?? [];

        var candidates = releases
            .Where(release => !release.Draft)
            .Select(release => new
            {
                Release = release,
                Parsed = SemanticVersion.TryParse(release.TagName, out var version) ? version : null
            })
            .Where(candidate => candidate.Parsed is not null
                && candidate.Parsed > AppBuildInfo.Version
                && UpdateChannelPolicy.Includes(channel, candidate.Parsed))
            .OrderByDescending(candidate => candidate.Parsed)
            .ToList();

        foreach (var candidate in candidates)
        {
            var manifestAsset = candidate.Release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, "update-manifest.json", StringComparison.OrdinalIgnoreCase));
            if (manifestAsset is null)
            {
                continue;
            }

            var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(
                manifestAsset.BrowserDownloadUrl,
                cancellationToken);
            if (manifest is null
                || manifest.SchemaVersion != 1
                || !SemanticVersion.TryParse(manifest.Version, out var manifestVersion)
                || manifestVersion.CompareTo(candidate.Parsed!) != 0
                || !string.Equals(
                    manifest.Version,
                    candidate.Release.TagName.TrimStart('v'),
                    StringComparison.Ordinal))
            {
                continue;
            }

            var deployment = AppBuildInfo.Deployment.ToSlug();
            var asset = manifest.Assets.FirstOrDefault(item =>
                string.Equals(item.Runtime, AppBuildInfo.Runtime, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Deployment, deployment, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                continue;
            }

            return new UpdateCheckResult(
                candidate.Parsed!,
                candidate.Release.TagName,
                candidate.Release.HtmlUrl,
                asset);
        }

        return null;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; init; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;
    }
}
