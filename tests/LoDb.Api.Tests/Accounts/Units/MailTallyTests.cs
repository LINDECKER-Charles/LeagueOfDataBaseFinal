using LoDb.Api.Modules.Accounts.Links;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The text of a throttle row: the total of the account's e-mails, then the times still
/// within the window; a row written before the total was kept counts its times.
/// </summary>
public sealed class MailTallyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = Now.AddDays(-1);

    [Fact]
    public void TallyKeepsItsTotalAndOnlyTheTimesWithinTheWindow()
    {
        var tally = new MailTally(0, [])
            .Add(Now.AddDays(-3))
            .Add(Now.AddHours(-2))
            .Add(Now.AddMinutes(-1));

        var read = MailTally.Parse(tally.Format(), Since);

        Assert.Equal(3, read.Total);
        Assert.Equal([Now.AddHours(-2), Now.AddMinutes(-1)], read.Recent);
        Assert.Equal(4, MailTally.Parse(read.Add(Now).Format(), Since).Total);
    }

    [Fact]
    public void RowWithoutATotalCountsItsTimes()
    {
        var legacy = string.Join(
            ' ',
            Now.AddMinutes(-10).ToUnixTimeMilliseconds(),
            Now.AddMinutes(-5).ToUnixTimeMilliseconds());

        var read = MailTally.Parse(legacy, Since);

        Assert.Equal(2, read.Total);
        Assert.Equal([Now.AddMinutes(-10), Now.AddMinutes(-5)], read.Recent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x;")]
    public void MissingOrDamagedRowCountsWhatItCanRead(string? value)
    {
        var read = MailTally.Parse(value, Since);

        Assert.Equal(0, read.Total);
        Assert.Empty(read.Recent);
    }

    [Fact]
    public void TotalNeverReadsBelowTheTimesItHolds()
    {
        var value = "1;" + string.Join(
            ' ',
            Now.AddMinutes(-3).ToUnixTimeMilliseconds(),
            Now.AddMinutes(-2).ToUnixTimeMilliseconds());

        Assert.Equal(2, MailTally.Parse(value, Since).Total);
    }
}
