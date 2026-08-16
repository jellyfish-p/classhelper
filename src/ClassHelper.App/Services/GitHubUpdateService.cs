using System.IO;
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
    private readonly Uri? _ossBaseUri;
    private readonly bool _ownsHttpClient;

    public GitHubUpdateService(HttpClient? httpClient = null, string? ossBaseUrl = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _ossBaseUri = CreateBaseUri(ossBaseUrl ?? AppBuildInfo.OssBaseUrl);

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
        try
        {
            return await CheckGitHubAsync(channel, cancellationToken);
        }
        catch (Exception githubException) when (
            _ossBaseUri is not null
            && (githubException is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            try
            {
                return await CheckOssAsync(channel, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ossException)
            {
                throw new HttpRequestException(
                    "GitHub 与国内镜像均暂时不可用。",
                    new AggregateException(githubException, ossException));
            }
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<UpdateCheckResult?> CheckGitHubAsync(
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
            var result = CreateResult(
                manifest,
                channel,
                candidate.Parsed,
                candidate.Release.TagName,
                candidate.Release.HtmlUrl);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private async Task<UpdateCheckResult?> CheckOssAsync(
        UpdateChannel channel,
        CancellationToken cancellationToken)
    {
        var channelName = channel.ToString().ToLowerInvariant();
        var manifestUri = new Uri(_ossBaseUri!, $"channels/{channelName}/update-manifest.json");
        var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(
            manifestUri,
            cancellationToken);
        if (manifest is null)
        {
            throw new InvalidDataException("国内镜像返回了空更新清单。");
        }

        if (!SemanticVersion.TryParse(manifest.Version, out var version))
        {
            throw new InvalidDataException("国内镜像的更新清单版本无效。");
        }

        if (version.CompareTo(AppBuildInfo.Version) <= 0)
        {
            return null;
        }

        return CreateResult(manifest, channel, version, manifest.Tag, manifest.ReleaseUrl)
            ?? throw new InvalidDataException("国内镜像的更新清单与当前程序不兼容。");
    }

    private static UpdateCheckResult? CreateResult(
        UpdateManifest? manifest,
        UpdateChannel channel,
        SemanticVersion? expectedVersion,
        string expectedTag,
        string releaseUrl)
    {
        if (manifest is null
            || manifest.SchemaVersion != 1
            || expectedVersion is null
            || !SemanticVersion.TryParse(manifest.Version, out var manifestVersion)
            || manifestVersion.CompareTo(expectedVersion) != 0
            || !UpdateChannelPolicy.Includes(channel, manifestVersion)
            || !string.Equals(manifest.Version, expectedTag.TrimStart('v'), StringComparison.Ordinal))
        {
            return null;
        }

        var deployment = AppBuildInfo.Deployment.ToSlug();
        var asset = manifest.Assets.FirstOrDefault(item =>
            string.Equals(item.Runtime, AppBuildInfo.Runtime, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Deployment, deployment, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            return null;
        }

        return new UpdateCheckResult(
            manifestVersion,
            expectedTag,
            string.IsNullOrWhiteSpace(releaseUrl) ? manifest.ReleaseUrl : releaseUrl,
            asset);
    }

    private static Uri? CreateBaseUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate($"{value.Trim().TrimEnd('/')}/", UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return uri;
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
