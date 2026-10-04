using LoDb.Infrastructure.Analytics.Import;

namespace LoDb.Api.Cli.Analytics;

/// <summary>
/// <c>analytics import</c>: takes over the daily aggregates of the legacy stack at the
/// switch-over, as <c>import</c> rows of <c>analytics_daily</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>analytics import --source &lt;directory&gt; [--dry-run]</c>. The directory holds the
/// day files <c>{yyyy-MM-dd}.json</c> of the legacy storage (<c>analytics/daily</c>). A day
/// that already has an aggregate is left alone, so a run again adds only what is missing;
/// a file that is no day aggregate is refused and counted. The summary goes to the standard
/// output.
/// </para>
/// <para>
/// Exit code 0 once imported; 1 when the directory is missing or the database fails; 2 on
/// invalid arguments.
/// </para>
/// </remarks>
internal sealed class AnalyticsImportCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "analytics import";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        AnalyticsImportArguments options;
        try
        {
            options = AnalyticsImportArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync(
                $"{exception.Message} {AnalyticsImportArguments.Usage}");
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var import = services.GetRequiredService<ILegacyDailyImport>();
        try
        {
            var report = await import.ImportAsync(
                options.Source,
                options.DryRun,
                cancellationToken);
            await Console.Out.WriteLineAsync(Summary(report, options.DryRun));
            return CliExitCodes.Success;
        }
        catch (DirectoryNotFoundException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return CliExitCodes.Failure;
        }
    }

    private static string Summary(DailyImportReport report, bool dryRun) =>
        $"{(dryRun ? "Dry run: would import" : "Imported")} {report.Imported} days"
        + $" from {report.Files} files; {report.AlreadyPresent} already present,"
        + $" {report.Refused} files refused.";
}
