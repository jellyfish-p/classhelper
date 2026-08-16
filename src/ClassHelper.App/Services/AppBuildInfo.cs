using System.Reflection;
using System.Runtime.InteropServices;
using ClassHelper.Core.Updates;

namespace ClassHelper.App.Services;

public static class AppBuildInfo
{
    private static readonly Assembly AppAssembly = typeof(AppBuildInfo).Assembly;

    public static SemanticVersion Version { get; } = ReadVersion();

    public static string DisplayVersion => Version.ToString();

    public static UpdateDeployment Deployment { get; } = ReadDeployment();

    public static string? OssBaseUrl { get; } = ReadOssBaseUrl();

    public static string Runtime => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.X86 => "win-x86",
        Architecture.Arm64 => "win-arm64",
        _ => $"win-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}"
    };

    private static SemanticVersion ReadVersion()
    {
        var informationalVersion = AppAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return SemanticVersion.TryParse(informationalVersion, out var version)
            ? version
            : SemanticVersion.Parse("0.1.0");
    }

    private static UpdateDeployment ReadDeployment()
    {
        var metadata = AppAssembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "ClassHelperDeployment")?
            .Value;
        return string.Equals(metadata, "self-contained", StringComparison.OrdinalIgnoreCase)
            ? UpdateDeployment.SelfContained
            : UpdateDeployment.FrameworkDependent;
    }

    private static string? ReadOssBaseUrl()
    {
        var configuredUrl = Environment.GetEnvironmentVariable("CLASSHELPER_OSS_BASE_URL");
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = AppAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "ClassHelperOssBaseUrl")?
                .Value;
        }

        if (string.IsNullOrWhiteSpace(configuredUrl)
            || !Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }
}
