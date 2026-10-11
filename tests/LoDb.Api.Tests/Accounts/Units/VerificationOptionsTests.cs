using System.Globalization;
using LoDb.Api.Modules.Accounts;
using LoDb.Api.Modules.Accounts.Verification;
using LoDb.Api.Tests.Hosting.Support;
using Microsoft.AspNetCore.Builder;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// Bounds of the e-mail verification that would flood an address, never let the owner
/// verify, or keep an unverified account for ever stop the host.
/// </summary>
public sealed class VerificationOptionsTests
{
    private const string Resend = "LoDb:Accounts:VerificationResend:";
    private const string Purge = "LoDb:Accounts:UnverifiedPurge:";

    [Theory]
    [InlineData(Resend + "FreeResends", "101")]
    [InlineData(Resend + "Interval", "00:00:30")]
    public void ResendBoundOutOfRangeStopsTheHost(string key, string value) =>
        StartupValidation.RefusalOf<VerificationResendOptions>(
            Register,
            new Dictionary<string, string?> { [key] = value });

    [Theory]
    [InlineData(Purge + "After", "12:00:00")]
    [InlineData(Purge + "BatchSize", "0")]
    public void PurgeBoundOutOfRangeStopsTheHost(string key, string value) =>
        StartupValidation.RefusalOf<UnverifiedPurgeOptions>(
            Register,
            new Dictionary<string, string?> { [key] = value });

    [Theory]
    [InlineData("00:00:59", false)]
    [InlineData("00:01:00", true)]
    [InlineData("7.00:00:00", true)]
    [InlineData("7.00:00:01", false)]
    public void ResendIntervalLiesBetweenAMinuteAndAWeek(string interval, bool isUsable) =>
        Assert.Equal(
            isUsable,
            new VerificationResendOptions { Interval = Duration(interval) }.HasUsableInterval());

    [Theory]
    [InlineData("23:59:59", false)]
    [InlineData("1.00:00:00", true)]
    [InlineData("365.00:00:00", true)]
    [InlineData("365.00:00:01", false)]
    public void PurgeDelayLiesBetweenADayAndAYear(string after, bool isUsable) =>
        Assert.Equal(
            isUsable,
            new UnverifiedPurgeOptions { After = Duration(after) }.HasUsableDelay());

    private static TimeSpan Duration(string text) =>
        TimeSpan.Parse(text, CultureInfo.InvariantCulture);

    private static void Register(WebApplicationBuilder builder) =>
        builder.Services.AddAccounts(builder.Configuration);
}
