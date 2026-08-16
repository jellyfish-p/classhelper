using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.Services;

public enum UpdateDownloadSource
{
    GitHub,
    OssMirror,
    LocalCache
}

public sealed record UpdateDownloadProgress(
    UpdateDownloadSource Source,
    long BytesReceived,
    long? TotalBytes);

public sealed record UpdateDownloadResult(
    string FilePath,
    UpdateDownloadSource Source);

public sealed class UpdateDownloadException(string message, IReadOnlyList<Exception> attempts)
    : Exception(message, new AggregateException(attempts))
{
    public IReadOnlyList<Exception> Attempts { get; } = attempts;
}

public sealed class UpdateDownloadService : IDisposable
{
    private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(45);
    private readonly HttpClient _httpClient;
    private readonly string _downloadRoot;
    private readonly bool _ownsHttpClient;
    private readonly TimeSpan _responseTimeout;
    private readonly TimeSpan _readTimeout;

    public UpdateDownloadService(
        HttpClient? httpClient = null,
        string? downloadRoot = null,
        TimeSpan? responseTimeout = null,
        TimeSpan? readTimeout = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? CreateHttpClient();
        _downloadRoot = downloadRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassHelper",
            "Updates");
        _responseTimeout = responseTimeout ?? DefaultResponseTimeout;
        _readTimeout = readTimeout ?? DefaultReadTimeout;
    }

    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateAsset asset,
        SemanticVersion version,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateAsset(asset);
        var sources = CreateSources(asset);
        if (sources.Count == 0)
        {
            throw new InvalidDataException("更新清单没有可用的下载地址。");
        }

        var versionDirectory = Path.Combine(_downloadRoot, SanitizePathSegment(version.ToString()));
        Directory.CreateDirectory(versionDirectory);
        var destinationPath = Path.Combine(versionDirectory, asset.FileName);
        var partialPath = $"{destinationPath}.partial";

        if (await IsValidFileAsync(destinationPath, asset, cancellationToken))
        {
            progress?.Report(new UpdateDownloadProgress(
                UpdateDownloadSource.LocalCache,
                asset.Size,
                asset.Size > 0 ? asset.Size : null));
            return new UpdateDownloadResult(destinationPath, UpdateDownloadSource.LocalCache);
        }

        TryDelete(partialPath);
        TryDelete(destinationPath);
        var failures = new List<Exception>();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DownloadFromSourceAsync(
                    source,
                    asset,
                    partialPath,
                    progress,
                    cancellationToken);
                File.Move(partialPath, destinationPath, true);
                return new UpdateDownloadResult(destinationPath, source.Source);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryDelete(partialPath);
                throw;
            }
            catch (Exception exception) when (IsSourceFailure(exception))
            {
                failures.Add(new IOException($"{SourceLabel(source.Source)} 下载失败。", exception));
                TryDelete(partialPath);
            }
        }

        var message = sources.Count > 1
            ? "GitHub 与国内镜像均无法完成下载。"
            : "GitHub 无法完成下载，且当前版本没有配置国内镜像。";
        throw new UpdateDownloadException(message, failures);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(6)
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ClassHelper/{AppBuildInfo.DisplayVersion}");
        return client;
    }

    private async Task DownloadFromSourceAsync(
        DownloadSource source,
        UpdateAsset asset,
        string partialPath,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Uri);
        using var responseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseCancellation.CancelAfter(_responseTimeout);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            responseCancellation.Token);
        response.EnsureSuccessStatusCode();

        var responseLength = response.Content.Headers.ContentLength;
        var totalBytes = asset.Size > 0 ? asset.Size : responseLength;
        progress?.Report(new UpdateDownloadProgress(source.Source, 0, totalBytes));

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            partialPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long bytesReceived = 0;

        try
        {
            while (true)
            {
                var bytesRead = await responseStream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .AsTask()
                    .WaitAsync(_readTimeout, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                hasher.AppendData(buffer, 0, bytesRead);
                bytesReceived += bytesRead;
                if (asset.Size > 0 && bytesReceived > asset.Size)
                {
                    throw new InvalidDataException("下载文件大小超过更新清单记录值。");
                }

                progress?.Report(new UpdateDownloadProgress(source.Source, bytesReceived, totalBytes));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        await fileStream.FlushAsync(cancellationToken);
        if (asset.Size > 0 && bytesReceived != asset.Size)
        {
            throw new InvalidDataException($"下载文件大小不匹配：应为 {asset.Size} 字节，实际为 {bytesReceived} 字节。");
        }

        var actualHash = Convert.ToHexString(hasher.GetHashAndReset());
        if (!string.Equals(actualHash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("下载文件的 SHA-256 校验失败。");
        }
    }

    private static async Task<bool> IsValidFileAsync(
        string filePath,
        UpdateAsset asset,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        if (asset.Size > 0 && fileInfo.Length != asset.Size)
        {
            return false;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        return string.Equals(actualHash, asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateAsset(UpdateAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.FileName)
            || !string.Equals(Path.GetFileName(asset.FileName), asset.FileName, StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(asset.FileName), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("更新清单中的文件名无效。");
        }

        try
        {
            if (Convert.FromHexString(asset.Sha256).Length != 32)
            {
                throw new InvalidDataException("更新清单中的 SHA-256 无效。");
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("更新清单中的 SHA-256 无效。", exception);
        }

        if (asset.Size < 0)
        {
            throw new InvalidDataException("更新清单中的文件大小无效。");
        }
    }

    private static List<DownloadSource> CreateSources(UpdateAsset asset)
    {
        var sources = new List<DownloadSource>();
        AddSource(sources, asset.DownloadUrl, UpdateDownloadSource.GitHub);
        AddSource(sources, asset.MirrorDownloadUrl, UpdateDownloadSource.OssMirror);
        return sources;
    }

    private static void AddSource(
        List<DownloadSource> sources,
        string value,
        UpdateDownloadSource source)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || sources.Any(item => item.Uri == uri))
        {
            return;
        }

        sources.Add(new DownloadSource(source, uri));
    }

    private static bool IsSourceFailure(Exception exception) => exception is
        HttpRequestException
        or IOException
        or InvalidDataException
        or OperationCanceledException
        or TimeoutException;

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalidCharacters.Contains(character) ? '_' : character));
    }

    private static string SourceLabel(UpdateDownloadSource source) => source switch
    {
        UpdateDownloadSource.GitHub => "GitHub",
        UpdateDownloadSource.OssMirror => "国内镜像",
        UpdateDownloadSource.LocalCache => "本地缓存",
        _ => "未知来源"
    };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record DownloadSource(UpdateDownloadSource Source, Uri Uri);
}
