using System.Globalization;
using LoDb.Api.Modules.Builds;
using LoDb.Api.Modules.Builds.Votes;
using LoDb.Api.Tests.Hosting.Support;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// An age before voting that is negative, or long enough to shut voting, stops the host.
/// </summary>
public sealed class VoteOptionsTests
{
    private const string MinimumAccountAge = "LoDb:Builds:Votes:MinimumAccountAge";

    [Theory]
    [InlineData("-00:00:00.0000001", false)]
    [InlineData("00:00:00", true)]
    [InlineData("30.00:00:00", true)]
    [InlineData("30.00:00:00.0000001", false)]
    public void AgeIsUsableFromZeroToThirtyDays(string age, bool isUsable)
    {
        var options = new VoteOptions
        {
            MinimumAccountAge = TimeSpan.Parse(age, CultureInfo.InvariantCulture),
        };

        Assert.Equal(isUsable, options.HasUsableMinimumAge());
    }

    [Fact]
    public void AgeOutOfRangeStopsTheHost() =>
        StartupValidation.RefusalOf<VoteOptions>(
            static builder => builder.Services.AddBuilds(builder.Configuration),
            new Dictionary<string, string?> { [MinimumAccountAge] = "31.00:00:00" });
}
