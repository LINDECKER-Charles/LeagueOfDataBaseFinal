using LoDb.Api.Modules.Accounts.Security.Invitations;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// <c>admin mfa-invite --email &lt;address&gt;</c>: prints the invitation without which an
/// administrator who has no authenticator cannot enrol one, valid a few minutes.
/// </summary>
/// <remarks>
/// <para>
/// The enrolment never opens to a password alone: whoever holds the password of an
/// administrator without a second factor would otherwise bind an authenticator of their
/// own. Only the operator who runs this command on the host sees the invitation.
/// </para>
/// <para>
/// Exit code 0 once printed; 1 when no administrator has the e-mail, when the account
/// already has an authenticator (<c>admin mfa-reset</c> removes it), or when the database
/// fails; 2 on invalid arguments.
/// </para>
/// </remarks>
internal sealed partial class AdminMfaInviteCommand(
    UserManager<User> users,
    MfaInvitations invitations,
    ILogger<AdminMfaInviteCommand> logger) : ICliCommand
{
    public const string Usage = "Usage: admin mfa-invite --email <address>";

    public static string Name => "admin mfa-invite";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (await AdminEmailArguments.ReadAsync(arguments, Usage) is not { } options)
        {
            return CliExitCodes.Usage;
        }

        var user = await users.FindByEmailAsync(options.Email);
        if (await RefusalAsync(user, options.Email) is { } refusal)
        {
            await Console.Error.WriteLineAsync(refusal);
            return CliExitCodes.Failure;
        }

        var invitation = invitations.Issue(user!.Id);
        LogInvited(logger, user.Id);
        await Console.Out.WriteLineAsync($"{user.UserName} <{user.Email}>");
        await Console.Out.WriteLineAsync(
            AdminEnrolment.InvitationLine(invitation, invitations.Lifetime));
        await Console.Out.WriteLineAsync("Next steps:");
        var step = 0;
        foreach (var line in AdminEnrolment.Steps(invitation))
        {
            await Console.Out.WriteLineAsync($"  {++step}. {line}");
        }

        return CliExitCodes.Success;
    }

    private async Task<string?> RefusalAsync(User? user, string email)
    {
        if (user is null || !await users.IsInRoleAsync(user, Role.Admin))
        {
            return $"No administrator has the e-mail {email}.";
        }

        return user.TwoFactorEnabled
            ? $"{email} already has an authenticator: admin mfa-reset removes it first."
            : null;
    }

    [LoggerMessage(
        EventName = "admin.mfa_invitation.issued",
        Level = LogLevel.Information,
        Message = "An invitation to enrol an authenticator was issued to account {AccountId}.")]
    private static partial void LogInvited(ILogger logger, int accountId);
}
