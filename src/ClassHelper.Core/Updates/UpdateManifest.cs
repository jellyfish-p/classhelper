namespace ClassHelper.Core.Updates;

public enum UpdateDeployment
{
    SelfContained,
    FrameworkDependent
}

public sealed class UpdateManifest
{
    public int SchemaVersion { get; init; }

    public string Version { get; init; } = string.Empty;

    public string Tag { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public DateTimeOffset PublishedAt { get; init; }

    public string ReleaseUrl { get; init; } = string.Empty;

    public List<UpdateAsset> Assets { get; init; } = [];
}

public sealed class UpdateAsset
{
    public string Runtime { get; init; } = string.Empty;

    public string Deployment { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public long Size { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public string DownloadUrl { get; init; } = string.Empty;

    public string MirrorDownloadUrl { get; init; } = string.Empty;
}

public static class UpdateDeploymentExtensions
{
    public static string ToSlug(this UpdateDeployment deployment) => deployment switch
    {
        UpdateDeployment.SelfContained => "self-contained",
        UpdateDeployment.FrameworkDependent => "framework-dependent",
        _ => throw new ArgumentOutOfRangeException(nameof(deployment), deployment, null)
    };
}
