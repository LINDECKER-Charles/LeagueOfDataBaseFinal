using System.Globalization;

namespace LoDb.Api.Modules.Audit.Import;

/// <summary>
/// The day files of the legacy journal (<c>{yyyy-MM-dd}.ndjson</c>) across its directories,
/// one file per day.
/// </summary>
/// <remarks>
/// The local copy (<c>var/state/audit/events</c>) and the storage archive (<c>audit/</c>)
/// hold the same day while it is archived but not yet pruned: the first directory given
/// that holds a day provides it, so the local one comes first, as the legacy query service
/// preferred it.
/// </remarks>
internal static class LegacyJournalDays
{
    private const string Extension = ".ndjson";
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>The file of each day, oldest day first.</summary>
    /// <exception cref="DirectoryNotFoundException">A directory does not exist.</exception>
    public static SortedDictionary<DateOnly, string> Index(IEnumerable<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        var days = new SortedDictionary<DateOnly, string>();
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"No directory {directory}.");
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*" + Extension))
            {
                if (DayOf(file) is { } day)
                {
                    days.TryAdd(day, file);
                }
            }
        }

        return days;
    }

    private static DateOnly? DayOf(string file) =>
        DateOnly.TryParseExact(
            Path.GetFileNameWithoutExtension(file),
            DayFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var day)
            ? day
            : null;
}
