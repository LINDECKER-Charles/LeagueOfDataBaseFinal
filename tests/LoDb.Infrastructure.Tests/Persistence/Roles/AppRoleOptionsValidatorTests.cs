using LoDb.Infrastructure.Persistence.Roles;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// The role settings PostgreSQL would refuse, or a password whose verifier would not match
/// the client's proof, stop the host at startup.
/// </summary>
public sealed class AppRoleOptionsValidatorTests
{
    [Fact]
    public void DefaultsAreTheDecidedOnes()
    {
        var options = new AppRoleOptions();

        Assert.Equal(TimeSpan.FromSeconds(15), options.StatementTimeout);
        Assert.Equal(60, options.ConnectionLimit);
        Assert.Null(options.Password);
        Assert.True(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(AppRoleOptions.UnlimitedConnections)]
    [InlineData(1)]
    public void ConnectionLimitIsPositiveOrUnlimited(int limit) =>
        Assert.True(Validate(new AppRoleOptions { ConnectionLimit = limit }).Succeeded);

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void OtherConnectionLimitsAreRefused(int limit) =>
        Assert.Contains(
            nameof(AppRoleOptions.ConnectionLimit),
            Validate(new AppRoleOptions { ConnectionLimit = limit }).FailureMessage,
            StringComparison.Ordinal);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StatementTimeoutIsPositive(int milliseconds)
    {
        var options = new AppRoleOptions
        {
            StatementTimeout = TimeSpan.FromMilliseconds(milliseconds),
        };

        Assert.Contains(
            nameof(AppRoleOptions.StatementTimeout),
            Validate(options).FailureMessage,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("é-password")]
    [InlineData("tab\tpassword")]
    public void PasswordIsPrintableAscii(string password) =>
        Assert.Contains(
            nameof(AppRoleOptions.Password),
            Validate(new AppRoleOptions { Password = password }).FailureMessage,
            StringComparison.Ordinal);

    private static ValidateOptionsResult Validate(AppRoleOptions options) =>
        new AppRoleOptionsValidator().Validate(null, options);
}
