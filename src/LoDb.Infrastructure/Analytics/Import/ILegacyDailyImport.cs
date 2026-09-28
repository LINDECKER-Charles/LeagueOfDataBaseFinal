namespace LoDb.Infrastructure.Analytics.Import;

/// <summary>
/// Takes over the daily aggregates of the legacy stack, the files
/// <c>analytics/daily/{yyyy-MM-dd}.json</c> of its storage, as <c>import</c> rows of
/// <c>analytics_daily</c>. Its raw events, which hold addresses and user agents, are not.
/// </summary>
public interface ILegacyDailyImport
{
    /// <summary>
    /// Imports every day file of a directory whose day has no aggregate yet: run again, it
    /// adds only what is missing, and never replaces a row.
    /// </summary>
    /// <param name="directory">The folder of the day files.</param>
    /// <param name="dryRun">Reads and counts, writes nothing.</param>
    /// <param name="cancellationToken">Stops the import between two days.</param>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    Task<DailyImportReport> ImportAsync(
        string directory,
        bool dryRun,
        CancellationToken cancellationToken);
}
