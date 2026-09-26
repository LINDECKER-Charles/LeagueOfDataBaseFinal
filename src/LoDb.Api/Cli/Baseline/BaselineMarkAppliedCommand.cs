using LoDb.Infrastructure.Persistence.Baseline;

namespace LoDb.Api.Cli.Baseline;

/// <summary>
/// <c>baseline mark-applied</c>: records the baseline migration in the EF history of a
/// database created by Doctrine, without applying anything.
/// </summary>
/// <remarks>
/// <c>migrate</c> marks on its own; this command lets an operator mark a copy of production
/// and check it before any migration runs. It succeeds when the baseline is recorded, even
/// by an earlier run, and fails on an empty database or a schema that differs from
/// Doctrine's, which it leaves untouched. The arguments are host settings.
/// </remarks>
internal sealed class BaselineMarkAppliedCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "baseline mark-applied";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var migrator = services.GetRequiredService<DatabaseMigrator>();
        var report = await migrator.MarkBaselineAppliedAsync(cancellationToken);
        return report.Baseline is BaselineOutcome.AlreadyApplied or BaselineOutcome.Marked
            ? CliExitCodes.Success
            : CliExitCodes.Failure;
    }
}
