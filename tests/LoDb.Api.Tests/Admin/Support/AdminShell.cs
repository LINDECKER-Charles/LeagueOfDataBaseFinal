using System.Globalization;
using LoDb.Api.Cli;
using LoDb.Api.Modules.Accounts;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Support;

/// <summary>
/// Runs an <c>admin</c> command as an operator does in the API container, on the database of
/// <see cref="AccountsApp"/>: its standard input fed, its standard output captured.
/// </summary>
/// <remarks>The console is the process's: the tests that call it share
/// <see cref="ShellCommandsGroup"/>, so that two runs never swap it at once.</remarks>
internal static class AdminShell
{
    public static async Task<(int Exit, string Output)> RunAsync(
        AccountsApp app,
        string input,
        IReadOnlyList<string> commandLine)
    {
        var connection = app.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("LoDb");
        var (originalInput, originalOutput) = (Console.In, Console.Out);
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetIn(new StringReader(input));
        Console.SetOut(output);
        try
        {
            var exit = await CliRunner.RunAsync(
                [
                    .. commandLine,
                    $"--ConnectionStrings:LoDb={connection}",
                    "--Logging:LogLevel:Default=Warning",
                ],
                typeof(Program).Assembly,
                static (services, configuration) =>
                    services.AddLoDbPersistence(configuration).AddAccounts(configuration));
            return (exit, output.ToString());
        }
        finally
        {
            Console.SetIn(originalInput);
            Console.SetOut(originalOutput);
        }
    }
}
