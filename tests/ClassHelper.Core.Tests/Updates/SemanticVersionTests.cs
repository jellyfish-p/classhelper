using ClassHelper.Core.Updates;

namespace ClassHelper.Core.Tests.Updates;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("v0.1.0-rc1")]
    [InlineData("v0.1.0-rc.1+build.42")]
    [InlineData("1.0.0-alpha.beta")]
    [InlineData("999999999999999999999.0.0")]
    public void TryParse_AcceptsCompleteSemVer(string value)
    {
        Assert.True(SemanticVersion.TryParse(value, out _));
    }

    [Theory]
    [InlineData("v01.0.0")]
    [InlineData("v1.0")]
    [InlineData("v1.0.0-rc.01")]
    [InlineData("v1.0.0-alpha..1")]
    [InlineData("v1.0.0-alpha_1")]
    public void TryParse_RejectsInvalidSemVer(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
    }

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    public void CompareTo_FollowsSemVerPrecedence(string earlier, string later)
    {
        Assert.True(SemanticVersion.Parse(earlier) < SemanticVersion.Parse(later));
    }

    [Fact]
    public void CompareTo_IgnoresBuildMetadata()
    {
        var first = SemanticVersion.Parse("1.0.0+build.1");
        var second = SemanticVersion.Parse("1.0.0+build.2");

        Assert.Equal(0, first.CompareTo(second));
    }
}
