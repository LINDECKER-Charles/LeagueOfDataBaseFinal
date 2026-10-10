using LoDb.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// Bounds of the sign-in that would let one address lock an account, or refuse every
/// sign-in, stop the host; a second hashing permit asks for one failure less.
/// </summary>
public sealed class SignInLimitOptionsTests
{
    private const string Section = "LoDb:RateLimiting:";
    private const string HashingPermits = "LoDb:Accounts:Hashing:Permits";
    private const string FailuresPerIdentifier = Section + "SignInFailuresPerIdentifier";

    [Theory]
    [InlineData("SignInFailuresPerIdentifier", "5")]
    [InlineData("SignInFailuresPerIdentifier", "0")]
    [InlineData("SignInPerWindow", "0")]
    [InlineData("PasswordConfirmsPerWindow", "0")]
    [InlineData("AccountWindow", "00:00:01")]
    public async Task BoundOutOfRangeStopsTheHost(string key, string value)
    {
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { [Section + key] = value },
        };

        Assert.Throws<OptionsValidationException>(() => factory.Services);
    }

    // Attempts checked together under two permits may both fail past the bound.
    [Fact]
    public async Task SecondHashingPermitNeedsOneFailureLess()
    {
        await using var tooMany = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { [HashingPermits] = "2" },
        };
        await using var fitting = new ApiFactory
        {
            Settings = new Dictionary<string, string?>
            {
                [HashingPermits] = "2",
                [FailuresPerIdentifier] = "3",
            },
        };

        Assert.Throws<OptionsValidationException>(() => tooMany.Services);
        Assert.NotNull(fitting.Services);
    }
}
