using System.Numerics;
using System.Text.RegularExpressions;

namespace ClassHelper.Core.Updates;

public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private static readonly Regex VersionPattern = new(
        "^(?:v)?(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string[] _prereleaseIdentifiers;

    private SemanticVersion(
        BigInteger major,
        BigInteger minor,
        BigInteger patch,
        string? prerelease,
        string? buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        BuildMetadata = buildMetadata;
        _prereleaseIdentifiers = prerelease?.Split('.') ?? [];
    }

    public BigInteger Major { get; }

    public BigInteger Minor { get; }

    public BigInteger Patch { get; }

    public string? Prerelease { get; }

    public string? BuildMetadata { get; }

    public bool IsPrerelease => _prereleaseIdentifiers.Length > 0;

    public static SemanticVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' 不是有效的 SemVer 2.0.0 版本。");

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = VersionPattern.Match(value);
        if (!match.Success)
        {
            return false;
        }

        version = new SemanticVersion(
            BigInteger.Parse(match.Groups[1].Value),
            BigInteger.Parse(match.Groups[2].Value),
            BigInteger.Parse(match.Groups[3].Value),
            NullIfEmpty(match.Groups[4].Value),
            NullIfEmpty(match.Groups[5].Value));
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var coreComparison = Major.CompareTo(other.Major);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        coreComparison = Minor.CompareTo(other.Minor);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        coreComparison = Patch.CompareTo(other.Patch);
        if (coreComparison != 0)
        {
            return coreComparison;
        }

        if (!IsPrerelease || !other.IsPrerelease)
        {
            return IsPrerelease == other.IsPrerelease ? 0 : IsPrerelease ? -1 : 1;
        }

        var sharedLength = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
        for (var index = 0; index < sharedLength; index++)
        {
            var comparison = ComparePrereleaseIdentifier(
                _prereleaseIdentifiers[index],
                other._prereleaseIdentifiers[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
    }

    public bool Equals(SemanticVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Major);
        hash.Add(Minor);
        hash.Add(Patch);
        foreach (var identifier in _prereleaseIdentifiers)
        {
            hash.Add(identifier, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var value = $"{Major}.{Minor}.{Patch}";
        if (Prerelease is not null)
        {
            value += $"-{Prerelease}";
        }

        if (BuildMetadata is not null)
        {
            value += $"+{BuildMetadata}";
        }

        return value;
    }

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        var leftIsNumeric = BigInteger.TryParse(left, out var leftNumber);
        var rightIsNumeric = BigInteger.TryParse(right, out var rightNumber);

        if (leftIsNumeric && rightIsNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftIsNumeric != rightIsNumeric)
        {
            return leftIsNumeric ? -1 : 1;
        }

        return StringComparer.Ordinal.Compare(left, right);
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
