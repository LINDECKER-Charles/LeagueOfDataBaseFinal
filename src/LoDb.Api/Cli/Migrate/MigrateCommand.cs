using LoDb.Infrastructure.Persistence.Baseline;

namespace LoDb.Api.Cli.Migrate;

/// <summary>
/// <c>migrate</c>: brings the database to the latest migration. A database created by
/// Doctrine is first marked at the baseline, then the pending migrations are applied.
/// </summary>
/// <remarks>
/// Run by the ephemeral Compose service <c>migrate</c>, which the API waits for. A second run
/// applies nothing. A database that is neither empty nor exactly the Doctrine schema is left
/// untouched and the command fails, which keeps the API down. The arguments are host
/// settings, as for the API.
/// </remarks>
internal sealed class MigrateCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "migrate";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        // Resolved here rather than injected: a missing connection string then fails as the
        // one logged line of a failed command, not as a crash report.
        var migrator = services.GetRequiredService<DatabaseMigrator>();
        var report = await migrator.MigrateAsync(cancellationToken);
        return report.Baseline is BaselineOutcome.UnexpectedSchema
            ? CliExitCodes.Failure
            : CliExitCodes.Success;
    }
}
