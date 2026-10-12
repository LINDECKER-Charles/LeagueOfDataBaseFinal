using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>Refuses at startup a role setting PostgreSQL would refuse or misread.</summary>
internal sealed class AppRoleOptionsValidator : IValidateOptions<AppRoleOptions>
{
    // statement_timeout is an integer of milliseconds on the server.
    private static readonly TimeSpan MinStatementTimeout = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxStatementTimeout =
        TimeSpan.FromMilliseconds(int.MaxValue);

    public ValidateOptionsResult Validate(string? name, AppRoleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.StatementTimeout < MinStatementTimeout
            || options.StatementTimeout > MaxStatementTimeout)
        {
            failures.Add("StatementTimeout must lie between 1 ms and 24 days.");
        }

        if (options.ConnectionLimit is < 1 and not AppRoleOptions.UnlimitedConnections)
        {
            failures.Add(
                $"ConnectionLimit must be positive, or {AppRoleOptions.UnlimitedConnections}.");
        }

        if (options.Password is { } password && !RolePasswords.IsPrintableAscii(password))
        {
            failures.Add("Password must be printable ASCII.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
