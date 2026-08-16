using System.Net;
using System.Security.Cryptography;
using System.Text;
using ClassHelper.App.Services;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.Tests.Services;

public sealed class UpdateDownloadServiceTests : IDisposable
{
    private readonly string _downloadRoot = Path.Combine(
        Path.GetTempPath(),
        $"ClassHelper.UpdateDownloadServiceTests.{Guid.NewGuid():N}");

    [Fact]
    public async Task DownloadAsync_UsesGitHubWhenPrimaryDownloadSucceeds()
    {
        var payload = Encoding.UTF8.GetBytes("verified update");
        var handler = new RecordingHandler(request => CreateResponse(
            request.RequestUri == new Uri("https://github.example/update.exe")
                ? HttpStatusCode.OK
                : HttpStatusCode.NotFound,
            payload));
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        var result = await service.DownloadAsync(
            CreateAsset(payload),
            SemanticVersion.Parse("1.2.3"),
            null,
            CancellationToken.None);

        Assert.Equal(UpdateDownloadSource.GitHub, result.Source);
        Assert.Equal(payload, await File.ReadAllBytesAsync(result.FilePath));
        Assert.Equal(["https://github.example/update.exe"], handler.Requests);
    }

    [Fact]
    public async Task DownloadAsync_FallsBackToOssWhenGitHubFails()
    {
        var payload = Encoding.UTF8.GetBytes("verified mirror update");
        var handler = new RecordingHandler(request => request.RequestUri?.Host == "github.example"
            ? CreateResponse(HttpStatusCode.ServiceUnavailable, [])
            : CreateResponse(HttpStatusCode.OK, payload));
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        var result = await service.DownloadAsync(
            CreateAsset(payload),
            SemanticVersion.Parse("1.2.3"),
            null,
            CancellationToken.None);

        Assert.Equal(UpdateDownloadSource.OssMirror, result.Source);
        Assert.Equal(payload, await File.ReadAllBytesAsync(result.FilePath));
        Assert.Equal(
            ["https://github.example/update.exe", "https://oss.example/update.exe"],
            handler.Requests);
    }

    [Fact]
    public async Task DownloadAsync_FallsBackToOssWhenGitHubHashIsWrong()
    {
        var payload = Encoding.UTF8.GetBytes("verified mirror update");
        var invalidPayload = Encoding.UTF8.GetBytes("tampered primary file!");
        Assert.Equal(payload.Length, invalidPayload.Length);
        var handler = new RecordingHandler(request => request.RequestUri?.Host == "github.example"
            ? CreateResponse(HttpStatusCode.OK, invalidPayload)
            : CreateResponse(HttpStatusCode.OK, payload));
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);

        var result = await service.DownloadAsync(
            CreateAsset(payload),
            SemanticVersion.Parse("1.2.3"),
            null,
            CancellationToken.None);

        Assert.Equal(UpdateDownloadSource.OssMirror, result.Source);
        Assert.Equal(payload, await File.ReadAllBytesAsync(result.FilePath));
    }

    [Fact]
    public async Task DownloadAsync_ReusesPreviouslyVerifiedLocalFile()
    {
        var payload = Encoding.UTF8.GetBytes("verified cached update");
        var handler = new RecordingHandler(_ => CreateResponse(HttpStatusCode.OK, payload));
        using var httpClient = new HttpClient(handler);
        using var service = CreateService(httpClient);
        var asset = CreateAsset(payload);
        var version = SemanticVersion.Parse("1.2.3");

        _ = await service.DownloadAsync(asset, version, null, CancellationToken.None);
        var result = await service.DownloadAsync(asset, version, null, CancellationToken.None);

        Assert.Equal(UpdateDownloadSource.LocalCache, result.Source);
        Assert.Single(handler.Requests);
    }

    public void Dispose()
    {
        if (Directory.Exists(_downloadRoot))
        {
            Directory.Delete(_downloadRoot, true);
        }
    }

    private UpdateDownloadService CreateService(HttpClient httpClient) => new(
        httpClient,
        _downloadRoot,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1));

    private static UpdateAsset CreateAsset(byte[] payload) => new()
    {
        Runtime = "win-x64",
        Deployment = "self-contained",
        FileName = "ClassHelper-1.2.3-win-x64-self-contained.exe",
        Size = payload.Length,
        Sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
        DownloadUrl = "https://github.example/update.exe",
        MirrorDownloadUrl = "https://oss.example/update.exe"
    };

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, byte[] payload) => new(statusCode)
    {
        Content = new ByteArrayContent(payload)
    };

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
