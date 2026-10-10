using LoDb.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// Bounds of the sign-in that would let one address lock an account, or refuse every
/// sign-in, stop the host.
/// </summary>
public sealed class SignInLimitOptionsTests
{
    private const string Section = "LoDb:RateLimiting:";

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
}
