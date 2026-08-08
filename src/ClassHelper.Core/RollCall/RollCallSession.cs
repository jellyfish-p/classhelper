using System.Security.Cryptography;
using ClassHelper.Core.Scheduling;

namespace ClassHelper.Core.RollCall;

public enum RollCallMode
{
    IndependentRandom,
    BalancedRound
}

public interface IRandomIndexSource
{
    int Next(int exclusiveMaximum);
}

public sealed class CryptographicRandomIndexSource : IRandomIndexSource
{
    public int Next(int exclusiveMaximum) => RandomNumberGenerator.GetInt32(exclusiveMaximum);
}

public sealed class RollCallSession
{
    private readonly IReadOnlyList<RosterMember> _candidates;
    private readonly IRandomIndexSource _random;
    private readonly List<RosterMember> _remaining = [];

    public RollCallSession(
        IEnumerable<RosterMember> roster,
        RollCallMode mode,
        IRandomIndexSource? random = null)
    {
        ArgumentNullException.ThrowIfNull(roster);

        _candidates = roster.Where(member => member.IsActive).ToList();
        if (_candidates.Count == 0)
        {
            throw new ArgumentException("固定名单中没有可参与点名的成员。", nameof(roster));
        }

        Mode = mode;
        _random = random ?? new CryptographicRandomIndexSource();
        ResetRound();
    }

    public RollCallMode Mode { get; }

    public int CandidateCount => _candidates.Count;

    public int RemainingInRound => Mode == RollCallMode.IndependentRandom
        ? _candidates.Count
        : _remaining.Count;

    public RosterMember Draw()
    {
        if (Mode == RollCallMode.IndependentRandom)
        {
            return _candidates[_random.Next(_candidates.Count)];
        }

        if (_remaining.Count == 0)
        {
            ResetRound();
        }

        var index = _random.Next(_remaining.Count);
        var selected = _remaining[index];
        _remaining.RemoveAt(index);
        return selected;
    }

    public void ResetRound()
    {
        _remaining.Clear();
        _remaining.AddRange(_candidates);
    }
}
