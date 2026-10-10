using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// <c>admin mfa-reset --email &lt;address&gt;</c>: removes the authenticator and the recovery
/// codes of an account, and closes its sessions, for one whose device is lost.
/// </summary>
/// <remarks>
/// <para>
/// The account then signs in with its password alone; an administrator holds no admin
/// session until <c>admin mfa-invite</c> lets them enrol a new authenticator. Running it
/// again on an account without an authenticator changes nothing more than its sessions.
/// </para>
/// <para>
/// Exit code 0 once removed; 1 when no account has the e-mail or the database fails; 2 on
/// invalid arguments.
/// </para>
/// </remarks>
internal sealed partial class AdminMfaResetCommand(
    UserManager<User> users,
    ILogger<AdminMfaResetCommand> logger) : ICliCommand
{
    public const string Usage = "Usage: admin mfa-reset --email <address>";

    // No recovery code left: the next enrolment hands out new ones.
    private const int NoRecoveryCode = 0;

    public static string Name => "admin mfa-reset";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (await AdminEmailArguments.ReadAsync(arguments, Usage) is not { } options)
        {
            return CliExitCodes.Usage;
        }

        if (await users.FindByEmailAsync(options.Email) is not { } user)
        {
            await Console.Error.WriteLineAsync($"No account has the e-mail {options.Email}.");
            return CliExitCodes.Failure;
        }

        await RemoveAuthenticatorAsync(user);
        LogReset(logger, user.Id);
        await Console.Out.WriteLineAsync(
            $"Removed the authenticator of {user.UserName} <{user.Email}>, sessions closed.");
        await Console.Out.WriteLineAsync(await users.IsInRoleAsync(user, Role.Admin)
            ? $"Next step: admin mfa-invite --email {options.Email} to enrol a new one."
            : "Next step: the member enrols a new one from their account page.");
        return CliExitCodes.Success;
    }

    // A new key that no device holds, then a new security stamp: the sessions and the
    // tokens of the account stop at their next check.
    private async Task RemoveAuthenticatorAsync(User user)
    {
        AdminGrant.Succeed(
            await users.SetTwoFactorEnabledAsync(user, enabled: false),
            "turn the second factor off");
        AdminGrant.Succeed(await users.ResetAuthenticatorKeyAsync(user), "reset the key");
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user, NoRecoveryCode);
        AdminGrant.Succeed(await users.UpdateSecurityStampAsync(user), "close the sessions");
    }

    [LoggerMessage(
        EventName = "admin.mfa.reset",
        Level = LogLevel.Information,
        Message = "The authenticator of account {AccountId} was removed from the shell.")]
    private static partial void LogReset(ILogger logger, int accountId);
}
