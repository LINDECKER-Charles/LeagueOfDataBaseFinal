using LoDb.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// An age before voting that is negative, or long enough to shut voting, stops the host.
/// </summary>
public sealed class VoteOptionsTests
{
    private const string MinimumAccountAge = "LoDb:Builds:Votes:MinimumAccountAge";

    [Theory]
    [InlineData("-01:00:00")]
    [InlineData("31.00:00:00")]
    public async Task AgeOutOfRangeStopsTheHost(string age)
    {
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { [MinimumAccountAge] = age },
        };

        Assert.Throws<OptionsValidationException>(() => factory.Services);
    }
}
