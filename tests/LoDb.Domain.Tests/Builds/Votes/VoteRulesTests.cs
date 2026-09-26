using LoDb.Domain.Builds.Votes;

namespace LoDb.Domain.Tests.Builds.Votes;

/// <summary>A vote toggles, as the legacy vote repository tests state it.</summary>
public sealed class VoteRulesTests
{
    [Theory]
    [InlineData(VoteRules.None, VoteDirection.Up, 1)]
    [InlineData(VoteRules.None, VoteDirection.Down, -1)]
    [InlineData(1, VoteDirection.Down, -1)]
    [InlineData(-1, VoteDirection.Up, 1)]
    [InlineData(1, VoteDirection.Up, VoteRules.None)]
    [InlineData(-1, VoteDirection.Down, VoteRules.None)]
    public void TheSameVoteWithdrawsItTheOtherSwitchesIt(
        int current,
        VoteDirection cast,
        int expected) =>
        Assert.Equal(expected, VoteRules.Toggle(current, cast));

    [Theory]
    [InlineData("up", VoteDirection.Up)]
    [InlineData("down", VoteDirection.Down)]
    public void UpAndDownAreTheDirections(string code, VoteDirection expected)
    {
        Assert.True(VoteRules.TryParse(code, out var direction));
        Assert.Equal(expected, direction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Up")]
    [InlineData("1")]
    [InlineData("sideways")]
    public void AnythingElseIsNoVote(string? code) => Assert.False(VoteRules.TryParse(code, out _));
}
