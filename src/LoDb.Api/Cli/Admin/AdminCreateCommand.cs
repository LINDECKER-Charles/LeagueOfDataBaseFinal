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
/// enrols an authenticator at their first sign-in.
/// </para>
/// <para>
/// Exit code 0 once the account is an administrator; 1 when the database or Identity
/// fails; 2 on invalid arguments.
/// </para>
/// </remarks>
internal sealed class AdminCreateCommand(IServiceProvider services) : ICliCommand
{
    public const string SignInPath = "/admin/login";

    public static string Name => "admin create";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        AdminCreateArguments options;
        try
        {
            options = AdminCreateArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"{exception.Message} {AdminCreateArguments.Usage}");
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var grant = ActivatorUtilities.CreateInstance<AdminGrant>(services);
        var result = await grant.GrantAsync(options.Email, cancellationToken);
        foreach (var line in Steps(result))
        {
            await Console.Out.WriteLineAsync(line);
        }

        return CliExitCodes.Success;
    }

    private static IEnumerable<string> Steps(AdminGrantResult result)
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

        yield return "Next steps:";
        var steps = NextSteps(result);
        for (var index = 0; index < steps.Count; index++)
        {
            yield return $"  {index + 1}. {steps[index]}";
        }
    }

    private static List<string> NextSteps(AdminGrantResult result)
    {
        List<string> steps = [$"Sign in at {SignInPath} with this e-mail and its password."];
        if (result.TwoFactorEnabled)
        {
            steps.Add("Enter the code of the authenticator already linked to the account.");
            return steps;
        }

        steps.Add("Scan the QR code with an authenticator app, then enter its code.");
        steps.Add("Keep the recovery codes, shown once, away from the authenticator.");
        if (result.Password is not null)
        {
            steps.Add("Change the password from the account page.");
        }

        return steps;
    }
}
