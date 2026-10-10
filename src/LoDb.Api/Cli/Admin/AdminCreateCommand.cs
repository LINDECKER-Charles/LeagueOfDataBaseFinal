using LoDb.Api.Modules.Accounts.Security.Invitations;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// <c>admin create --email &lt;address&gt;</c>: makes the account of an e-mail an
/// administrator, creating it if needed, and prints what the administrator does next.
/// </summary>
/// <remarks>
/// <para>
/// The way to the first administrator, whom no one can promote from the admin, and to the
/// next ones. A created account gets a random password, printed once: the operator hands it
/// over and the administrator changes it. Nothing opens the admin before the administrator
/// enrols an authenticator, with the invitation printed here for an account without one
/// (<c>admin mfa-invite</c> prints another once it expires).
/// </para>
/// <para>
/// Exit code 0 once the account is an administrator; 1 when the database or Identity
/// fails; 2 on invalid arguments.
/// </para>
/// </remarks>
internal sealed class AdminCreateCommand(IServiceProvider services) : ICliCommand
{
    public const string SignInPath = "/admin/login";
    public const string Usage = "Usage: admin create --email <address>";

    public static string Name => "admin create";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (await AdminEmailArguments.ReadAsync(arguments, Usage) is not { } options)
        {
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var grant = ActivatorUtilities.CreateInstance<AdminGrant>(services);
        var result = await grant.GrantAsync(options.Email, cancellationToken);
        var invitations = services.GetRequiredService<MfaInvitations>();
        var invitation = result.TwoFactorEnabled ? null : invitations.Issue(result.AccountId);
        foreach (var line in Lines(result, invitation, invitations.Lifetime))
        {
            await Console.Out.WriteLineAsync(line);
        }

        return CliExitCodes.Success;
    }

    private static IEnumerable<string> Lines(
        AdminGrantResult result,
        string? invitation,
        TimeSpan lifetime)
    {
        var account = $"{result.Username} <{result.Email}>";
        yield return result switch
        {
            { Password: not null } => $"Created the administrator account {account}.",
            { Promoted: true } => $"Made {account} an administrator.",
            _ => $"{account} already is an administrator.",
        };
        if (result.Password is { } password)
        {
            yield return $"Password, shown only now: {password}";
        }

        if (invitation is not null)
        {
            yield return AdminEnrolment.InvitationLine(invitation, lifetime);
        }

        yield return "Next steps:";
        var steps = NextSteps(result, invitation);
        for (var index = 0; index < steps.Count; index++)
        {
            yield return $"  {index + 1}. {steps[index]}";
        }
    }

    private static List<string> NextSteps(AdminGrantResult result, string? invitation)
    {
        if (invitation is null)
        {
            return
            [
                $"Sign in at {SignInPath} with this e-mail and its password.",
                "Enter the code of the authenticator already linked to the account.",
            ];
        }

        List<string> steps = [.. AdminEnrolment.Steps(invitation)];
        if (result.Password is not null)
        {
            steps.Add("Change the password from the account page.");
        }

        return steps;
    }
}
