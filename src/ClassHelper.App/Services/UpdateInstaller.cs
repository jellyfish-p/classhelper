using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace ClassHelper.App.Services;

public static class UpdateInstaller
{
    private const string ApplyUpdateArgument = "--classhelper-apply-update";
    private const string CleanupUpdateArgument = "--classhelper-cleanup-update";

    public static bool IsApplyUpdateRequest(IReadOnlyList<string> arguments) =>
        arguments.Count > 0
        && string.Equals(arguments[0], ApplyUpdateArgument, StringComparison.OrdinalIgnoreCase);

    public static bool IsCleanupUpdateRequest(IReadOnlyList<string> arguments) =>
        arguments.Count > 0
        && string.Equals(arguments[0], CleanupUpdateArgument, StringComparison.OrdinalIgnoreCase);

    public static Process StartInstall(string stagedExecutable, string expectedSha256)
    {
        var sourcePath = Path.GetFullPath(stagedExecutable);
        var targetPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定当前程序路径。");
        targetPath = Path.GetFullPath(targetPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("已下载的更新文件不存在。", sourcePath);
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("更新文件不能与当前程序使用同一路径。");
        }

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("无法确定当前程序目录。");
        var startInfo = new ProcessStartInfo(sourcePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(sourcePath)
        };
        if (!CanWriteDirectory(targetDirectory))
        {
            startInfo.Verb = "runas";
        }

        startInfo.ArgumentList.Add(ApplyUpdateArgument);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(targetPath);
        startInfo.ArgumentList.Add(expectedSha256);
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动更新安装程序。");
    }

    public static async Task ApplyUpdateAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkerArguments(arguments, ApplyUpdateArgument, out var parentProcessId, out var targetPath, out var expectedSha256))
        {
            throw new InvalidDataException("更新安装参数无效。");
        }

        var sourcePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定更新程序路径。");
        sourcePath = Path.GetFullPath(sourcePath);
        targetPath = Path.GetFullPath(targetPath);

        await VerifyHashAsync(sourcePath, expectedSha256, cancellationToken);
        await WaitForProcessExitAsync(parentProcessId, TimeSpan.FromMinutes(2), cancellationToken);

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("无法确定安装目录。");
        Directory.CreateDirectory(targetDirectory);
        var operationId = Guid.NewGuid().ToString("N");
        var pendingPath = Path.Combine(targetDirectory, $".{Path.GetFileName(targetPath)}.{operationId}.pending");
        var backupPath = Path.Combine(targetDirectory, $".{Path.GetFileName(targetPath)}.{operationId}.backup");
        var replacedExistingFile = File.Exists(targetPath);

        try
        {
            File.Copy(sourcePath, pendingPath, true);
            if (replacedExistingFile)
            {
                File.Replace(pendingPath, targetPath, backupPath, true);
            }
            else
            {
                File.Move(pendingPath, targetPath);
            }

            var startInfo = new ProcessStartInfo(targetPath)
            {
                UseShellExecute = true,
                WorkingDirectory = targetDirectory
            };
            startInfo.ArgumentList.Add(CleanupUpdateArgument);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add(sourcePath);
            startInfo.ArgumentList.Add(replacedExistingFile ? backupPath : string.Empty);
            _ = Process.Start(startInfo)
                ?? throw new InvalidOperationException("更新已安装，但无法重新启动课堂助手。");
        }
        catch
        {
            TryDelete(pendingPath);
            if (replacedExistingFile && File.Exists(backupPath))
            {
                File.Copy(backupPath, targetPath, true);
                TryDelete(backupPath);
            }

            throw;
        }
    }

    public static async Task CleanupUpdateAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkerArguments(arguments, CleanupUpdateArgument, out var parentProcessId, out var stagedPath, out var backupPath))
        {
            return;
        }

        try
        {
            await WaitForProcessExitAsync(parentProcessId, TimeSpan.FromSeconds(20), cancellationToken);
        }
        catch (TimeoutException)
        {
            return;
        }

        await TryDeleteWithRetryAsync(stagedPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            await TryDeleteWithRetryAsync(backupPath, cancellationToken);
        }

        var stagingDirectory = Path.GetDirectoryName(stagedPath);
        if (stagingDirectory is not null)
        {
            try
            {
                Directory.Delete(stagingDirectory, false);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task VerifyHashAsync(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedSha256);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("更新文件校验值无效。", exception);
        }

        if (expectedHash.Length != 32)
        {
            throw new InvalidDataException("更新文件校验值无效。");
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            throw new InvalidDataException("安装前 SHA-256 复核失败，更新已终止。");
        }
    }

    private static async Task WaitForProcessExitAsync(
        int processId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (process)
        using (var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeoutCancellation.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("等待旧版本退出超时。");
            }
        }
    }

    private static bool TryParseWorkerArguments(
        IReadOnlyList<string> arguments,
        string command,
        out int processId,
        out string firstValue,
        out string secondValue)
    {
        processId = 0;
        firstValue = string.Empty;
        secondValue = string.Empty;
        if (arguments.Count != 4
            || !string.Equals(arguments[0], command, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(arguments[1], out processId)
            || processId <= 0
            || string.IsNullOrWhiteSpace(arguments[2]))
        {
            return false;
        }

        firstValue = arguments[2];
        secondValue = arguments[3];
        return true;
    }

    private static bool CanWriteDirectory(string directory)
    {
        var probePath = Path.Combine(directory, $".classhelper-write-{Guid.NewGuid():N}.tmp");
        try
        {
            using var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            TryDelete(probePath);
        }
    }

    private static async Task TryDeleteWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

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
}
