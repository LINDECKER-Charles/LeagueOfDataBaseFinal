using LoDb.Api.Modules.Audit.Import;

namespace LoDb.Api.Cli.Audit;

/// <summary>
/// <c>audit import</c>: copies the last six months of the legacy journal into
/// <c>audit_log</c> at the switch-over.
/// </summary>
/// <remarks>
/// <para>
/// <c>audit import --source &lt;directory&gt; [--source &lt;directory&gt;…] [--dry-run]</c>.
/// Each directory holds day files <c>{yyyy-MM-dd}.ndjson</c>: the local copy of the legacy
/// stack (<c>var/state/audit/events</c>) first, then its storage archive
/// (<c>&lt;storage&gt;/audit</c>). Days older than the retention are left out. Run again,
/// it adds only what is missing. The summary goes to the standard output.
/// </para>
/// <para>
/// Exit code 0 once imported; 1 when a directory is missing or the database fails; 2 on
/// invalid arguments.
/// </para>
/// </remarks>
internal sealed class AuditImportCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "audit import";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        AuditImportArguments options;
        try
        {
            options = AuditImportArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"{exception.Message} {AuditImportArguments.Usage}");
            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var import = services.GetRequiredService<LegacyAuditImport>();
        try
        {
            var report = await import.ImportAsync(
                options.Sources,
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

    private static string Summary(AuditImportReport report, bool dryRun) =>
        $"{(dryRun ? "Dry run: would import" : "Imported")} {report.Imported} entries"
        + $" from {report.Days} days; {report.AlreadyPresent} already present,"
        + $" {report.Refused} lines refused, {report.ExpiredDays} days past the retention.";
}
