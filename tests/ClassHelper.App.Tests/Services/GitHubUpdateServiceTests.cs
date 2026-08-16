using System.Net;
using System.Net.Http.Json;
using ClassHelper.App.Services;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.Tests.Services;

public sealed class GitHubUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_FallsBackToOssManifestWhenGitHubFails()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 1,
            Version = "1.2.3",
            Tag = "v1.2.3",
            Channel = "stable",
            ReleaseUrl = "https://github.example/releases/v1.2.3",
            Assets =
            [
                new UpdateAsset
                {
                    Runtime = AppBuildInfo.Runtime,
                    Deployment = AppBuildInfo.Deployment.ToSlug(),
                    FileName = "ClassHelper.exe",
                    Size = 10,
                    Sha256 = new string('0', 64),
                    DownloadUrl = "https://github.example/ClassHelper.exe",
                    MirrorDownloadUrl = "https://oss.example/ClassHelper.exe"
                }
            ]
        };
        var handler = new RecordingHandler(request => request.RequestUri?.Host == "api.github.com"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(manifest) });
        using var httpClient = new HttpClient(handler);
        using var service = new GitHubUpdateService(httpClient, "https://oss.example/root");

        var result = await service.CheckAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1.2.3", result.Version.ToString());
        Assert.Equal("https://oss.example/ClassHelper.exe", result.Asset.MirrorDownloadUrl);
        Assert.Equal(
            [
                "https://api.github.com/repos/jellyfish-p/classhelper/releases?per_page=30",
                "https://oss.example/root/channels/stable/update-manifest.json"
            ],
            handler.Requests);
    }

    [Fact]
    public async Task CheckAsync_DoesNotUseOssWhenGitHubCheckSucceeds()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        });
        using var httpClient = new HttpClient(handler);
        using var service = new GitHubUpdateService(httpClient, "https://oss.example/root");

        var result = await service.CheckAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(
            ["https://api.github.com/repos/jellyfish-p/classhelper/releases?per_page=30"],
            handler.Requests);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(responder(request));
        }
    }
}
