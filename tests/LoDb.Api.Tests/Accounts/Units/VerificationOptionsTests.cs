using LoDb.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// Bounds of the e-mail verification that would flood an address, never let the owner
/// verify, or keep an unverified account for ever stop the host.
/// </summary>
public sealed class VerificationOptionsTests
{
    [Theory]
    [InlineData("LoDb:Accounts:VerificationResend:FreeResends", "-1")]
    [InlineData("LoDb:Accounts:VerificationResend:FreeResends", "101")]
    [InlineData("LoDb:Accounts:VerificationResend:Interval", "00:00:30")]
    [InlineData("LoDb:Accounts:VerificationResend:Interval", "8.00:00:00")]
    [InlineData("LoDb:Accounts:UnverifiedPurge:After", "12:00:00")]
    [InlineData("LoDb:Accounts:UnverifiedPurge:After", "400.00:00:00")]
    [InlineData("LoDb:Accounts:UnverifiedPurge:BatchSize", "0")]
    public async Task BoundOutOfRangeStopsTheHost(string key, string value)
    {
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { [key] = value },
        };

        Assert.Throws<OptionsValidationException>(() => factory.Services);
    }
}
