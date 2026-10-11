using LoDb.Infrastructure.Audience;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Audience;

/// <summary>
/// The pull's settings: missing ones leave it dormant and pass, a host that is not a bare
/// name and bounds that would let it run unbounded are refused at startup.
/// </summary>
public sealed class AudienceOptionsValidatorTests
{
    [Fact]
    public void DefaultsAreAcceptedAndDormant()
    {
        var options = new AudienceOptions();

        Assert.True(Validate(options).Succeeded);
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void TheFourSettingsWakeThePull()
    {
        var options = Configured();

        Assert.True(Validate(options).Succeeded);
        Assert.True(options.IsConfigured);
        Assert.False(Configured(password: string.Empty).IsConfigured);
    }

    [Theory]
    [InlineData("https://stats.example.test")]
    [InlineData("stats.example.test:8443")]
    [InlineData("stats.example.test/api")]
    [InlineData("10.0.0.1")]
    public void AHostThatIsNotABareNameIsRefused(string host)
    {
        var result = Validate(Configured(host));

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(AudienceOptions.Host),
            result.FailureMessage,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(AudienceOptions.RefreshDays))]
    [InlineData(nameof(AudienceOptions.PageSize))]
    [InlineData(nameof(AudienceOptions.MaxPages))]
    [InlineData(nameof(AudienceOptions.ClipWindowDays))]
    public void ABoundUnderOneIsRefused(string bound)
    {
        var options = Configured();
        typeof(AudienceOptions).GetProperty(bound)!.SetValue(options, 0);

        var result = Validate(options);

        Assert.True(result.Failed);
        Assert.Contains(bound, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AClipFactorUnderOneIsRefused(double factor)
    {
        var options = Configured();
        options.ClipFactor = factor;

        Assert.True(Validate(options).Failed);
    }

    private static ValidateOptionsResult Validate(AudienceOptions options) =>
        new AudienceOptionsValidator().Validate(null, options);

    private static AudienceOptions Configured(
        string host = "stats.example.test",
        string password = "secret") =>
        new()
        {
            Host = host,
            WebsiteId = Guid.Parse("deee1958-0f6c-4a32-a327-aad389836c3b"),
            Username = "trends-reader",
            Password = password,
        };
}
