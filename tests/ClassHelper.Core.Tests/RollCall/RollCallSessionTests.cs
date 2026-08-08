using ClassHelper.Core.RollCall;
using ClassHelper.Core.Scheduling;

namespace ClassHelper.Core.Tests.RollCall;

public sealed class RollCallSessionTests
{
    [Fact]
    public void IndependentRandom_AllowsSameMemberOnConsecutiveDraws()
    {
        var roster = CreateRoster("甲", "乙", "丙");
        var session = new RollCallSession(roster, RollCallMode.IndependentRandom, new FirstIndexSource());

        var first = session.Draw();
        var second = session.Draw();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(3, session.RemainingInRound);
    }

    [Fact]
    public void BalancedRound_DoesNotRepeatBeforeRoundIsExhausted()
    {
        var roster = CreateRoster("甲", "乙", "丙");
        var session = new RollCallSession(roster, RollCallMode.BalancedRound, new FirstIndexSource());

        var results = Enumerable.Range(0, 3).Select(_ => session.Draw()).ToList();

        Assert.Equal(3, results.Select(result => result.Id).Distinct().Count());
        Assert.Equal(0, session.RemainingInRound);
    }

    [Fact]
    public void BalancedRound_StartsNewRoundAfterExhaustion()
    {
        var roster = CreateRoster("甲", "乙");
        var session = new RollCallSession(roster, RollCallMode.BalancedRound, new FirstIndexSource());

        _ = session.Draw();
        _ = session.Draw();
        var nextRound = session.Draw();

        Assert.Equal(roster[0].Id, nextRound.Id);
        Assert.Equal(1, session.RemainingInRound);
    }

    [Fact]
    public void Session_ExcludesInactiveMembers()
    {
        var active = new RosterMember(Guid.NewGuid(), "参与者", "01", true);
        var inactive = new RosterMember(Guid.NewGuid(), "暂不参与", "02", false);
        var session = new RollCallSession([inactive, active], RollCallMode.IndependentRandom, new FirstIndexSource());

        Assert.Equal(active.Id, session.Draw().Id);
        Assert.Equal(1, session.CandidateCount);
    }

    [Fact]
    public void Session_RejectsRosterWithoutActiveMembers()
    {
        var roster = new[] { new RosterMember(Guid.NewGuid(), "未启用", null, false) };

        var exception = Assert.Throws<ArgumentException>(() =>
            new RollCallSession(roster, RollCallMode.BalancedRound, new FirstIndexSource()));

        Assert.Contains("没有可参与点名", exception.Message, StringComparison.Ordinal);
    }

    private static List<RosterMember> CreateRoster(params string[] names) =>
        names.Select(name => new RosterMember(Guid.NewGuid(), name)).ToList();

    private sealed class FirstIndexSource : IRandomIndexSource
    {
        public int Next(int exclusiveMaximum)
        {
            Assert.True(exclusiveMaximum > 0);
            return 0;
        }
    }
}
