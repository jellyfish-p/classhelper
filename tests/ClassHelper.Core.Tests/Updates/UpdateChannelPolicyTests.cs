using ClassHelper.Core.Updates;

namespace ClassHelper.Core.Tests.Updates;

public sealed class UpdateChannelPolicyTests
{
    [Theory]
    [InlineData("1.0.0-alpha.1", UpdateChannel.Alpha)]
    [InlineData("1.0.0-beta.1", UpdateChannel.Beta)]
    [InlineData("1.0.0-rc.1", UpdateChannel.Prerelease)]
    [InlineData("1.0.0-preview.2", UpdateChannel.Prerelease)]
    [InlineData("1.0.0", UpdateChannel.Stable)]
    public void ForInstalledVersion_UsesInstalledStability(string version, UpdateChannel expected)
    {
        Assert.Equal(expected, UpdateChannelPolicy.ForInstalledVersion(SemanticVersion.Parse(version)));
    }

    [Fact]
    public void Includes_UsesSelectedChannelAsMinimumStability()
    {
        var alpha = SemanticVersion.Parse("1.0.0-alpha.1");
        var beta = SemanticVersion.Parse("1.0.0-beta.1");
        var candidate = SemanticVersion.Parse("1.0.0-rc.1");
        var stable = SemanticVersion.Parse("1.0.0");

        Assert.All(new[] { alpha, beta, candidate, stable }, version =>
            Assert.True(UpdateChannelPolicy.Includes(UpdateChannel.Alpha, version)));
        Assert.False(UpdateChannelPolicy.Includes(UpdateChannel.Beta, alpha));
        Assert.True(UpdateChannelPolicy.Includes(UpdateChannel.Beta, stable));
        Assert.False(UpdateChannelPolicy.Includes(UpdateChannel.Stable, candidate));
        Assert.True(UpdateChannelPolicy.Includes(UpdateChannel.Stable, stable));
    }
}
