using System.Globalization;
using LoDb.Api.Cli;
using LoDb.Api.Modules.Accounts;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.DataProtection;
using LoDb.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin.Support;

/// <summary>
/// Runs an <c>admin</c> command as an operator does in the API container, on the database of
/// <see cref="AccountsApp"/>: its standard input fed, its standard output captured.
/// </summary>
/// <remarks>
/// <para>
/// The console is the process's: the tests that call it share
/// <see cref="ShellCommandsGroup"/>, so that two runs never swap it at once.
/// </para>
/// <para>
/// The command host reads the key ring of the database, as on a served host: the API,
/// started first, created its key, so what a command seals the API reads.
/// </para>
/// </remarks>
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
                static (services, configuration) => services
                    .AddLoDbPersistence(configuration)
                    .AddLoDbDataProtection(configuration)
                    .AddAccounts(configuration));
            return (exit, output.ToString());
        }
        finally
        {
            Console.SetIn(originalInput);
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>What the one line of <paramref name="output"/> that starts with
    /// <paramref name="label"/> gives after it.</summary>
    public static string ValueOf(string output, string label) =>
        output.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Single(line => line.StartsWith(label, StringComparison.Ordinal))
            [label.Length..];
}
