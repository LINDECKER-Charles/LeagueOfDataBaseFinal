using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Security.Limits;
using LoDb.Api.Tests.Hosting.Support;
using Microsoft.AspNetCore.Builder;

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
    [InlineData("SignInPerWindow", "0")]
    [InlineData("AccountWindow", "00:00:01")]
    public void BoundOutOfRangeStopsTheHost(string key, string value) =>
        StartupValidation.RefusalOf<SignInLimitOptions>(
            Register,
            new Dictionary<string, string?> { [Section + key] = value });

    // Attempts checked together under two permits may both fail past the bound.
    [Fact]
    public void SecondHashingPermitNeedsOneFailureLess()
    {
        StartupValidation.RefusalOf<SignInLimitOptions>(
            Register,
            new Dictionary<string, string?> { [HashingPermits] = "2" });
        StartupValidation.Run(
            Register,
            new Dictionary<string, string?>
            {
                [HashingPermits] = "2",
                [FailuresPerIdentifier] = "3",
            });
    }

    private static void Register(WebApplicationBuilder builder) =>
        builder.Services.AddAccountsProtection(builder.Configuration);
}
