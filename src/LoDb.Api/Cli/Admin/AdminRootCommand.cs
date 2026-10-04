namespace LoDb.Api.Cli.Admin;

/// <summary>
/// <c>admin root --username &lt;name&gt; --email &lt;address&gt;</c>, the password on the
/// standard input: makes sure the root administrator exists with that password and the
/// admin role. The deploy job runs it from the host's .env (LODB_ADMIN_*).
/// </summary>
/// <remarks>
/// Exit code 0 once the account is the root administrator; 1 when the database or Identity
/// fails, or the username belongs to another account; 2 on invalid arguments or password.
/// The output never holds the password.
/// </remarks>
internal sealed class AdminRootCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "admin root";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        AdminRootArguments options;
        string password;
        try
        {
            options = AdminRootArguments.Parse(arguments);
            password = AdminRootArguments.Password(
                await Console.In.ReadLineAsync(cancellationToken));
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"{exception.Message} {AdminRootArguments.Usage}");
            return CliExitCodes.Usage;
        }

        var grant = ActivatorUtilities.CreateInstance<AdminGrant>(services);
        var root = ActivatorUtilities.CreateInstance<AdminRoot>(services, grant);
        var result = await root.EnsureAsync(options, password);
        await Console.Out.WriteLineAsync(Summary(options.Username, result));
        return CliExitCodes.Success;
    }

    private static string Summary(string username, AdminRootResult result) => result switch
    {
        { Created: true } => $"Created the root administrator {username}.",
        { PasswordChanged: true } => $"Set the password of the root administrator {username}.",
        { Promoted: true } => $"Made {username} the root administrator.",
        _ => $"{username} already is the root administrator.",
    };
}
