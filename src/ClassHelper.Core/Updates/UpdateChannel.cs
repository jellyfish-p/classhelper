namespace ClassHelper.Core.Updates;

public enum UpdateChannel
{
    Stable,
    Prerelease,
    Beta,
    Alpha
}

public static class UpdateChannelPolicy
{
    public static UpdateChannel ForInstalledVersion(SemanticVersion version)
    {
        if (!version.IsPrerelease)
        {
            return UpdateChannel.Stable;
        }

        var firstIdentifier = version.Prerelease!.Split('.')[0];
        if (firstIdentifier.StartsWith("alpha", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateChannel.Alpha;
        }

        return firstIdentifier.StartsWith("beta", StringComparison.OrdinalIgnoreCase)
            ? UpdateChannel.Beta
            : UpdateChannel.Prerelease;
    }

    public static bool Includes(UpdateChannel selectedChannel, SemanticVersion candidate)
    {
        var candidateChannel = ForInstalledVersion(candidate);
        return Stability(candidateChannel) >= Stability(selectedChannel);
    }

    private static int Stability(UpdateChannel channel) => channel switch
    {
        UpdateChannel.Alpha => 0,
        UpdateChannel.Beta => 1,
        UpdateChannel.Prerelease => 2,
        UpdateChannel.Stable => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
    };
}
