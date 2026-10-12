using LoDb.Infrastructure.Persistence.Roles;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>A backup password whose verifier would not match stops the host at startup.</summary>
public sealed class BackupRoleOptionsValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("0f3a9c2e7b1d4e6f8a0b2c4d6e8f0a1b")]
    public void UnsetOrPrintablePasswordPasses(string? password) =>
        Assert.True(Validate(new BackupRoleOptions { Password = password }).Succeeded);

    [Theory]
    [InlineData("é-password")]
    [InlineData("tab\tpassword")]
    public void OtherPasswordsAreRefused(string password) =>
        Assert.Contains(
            nameof(BackupRoleOptions.Password),
            Validate(new BackupRoleOptions { Password = password }).FailureMessage,
            StringComparison.Ordinal);

    private static ValidateOptionsResult Validate(BackupRoleOptions options) =>
        new BackupRoleOptionsValidator().Validate(null, options);
}
