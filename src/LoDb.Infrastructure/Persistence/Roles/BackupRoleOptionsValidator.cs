using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>Refuses at startup a backup password whose verifier would not match.</summary>
internal sealed class BackupRoleOptionsValidator : IValidateOptions<BackupRoleOptions>
{
    public ValidateOptionsResult Validate(string? name, BackupRoleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Password is { } password && !RolePasswords.IsPrintableAscii(password)
            ? ValidateOptionsResult.Fail("Password must be printable ASCII.")
            : ValidateOptionsResult.Success;
    }
}
