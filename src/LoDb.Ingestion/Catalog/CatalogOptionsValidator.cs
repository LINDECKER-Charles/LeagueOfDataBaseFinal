using LoDb.Ingestion.Catalog.Reading;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Refuses at startup a catalog that could hold nothing, versions cached for too long, an
/// unreadable pinned language, or a cold gate that would admit nothing or never let go.
/// </summary>
internal sealed class CatalogOptionsValidator : IValidateOptions<CatalogOptions>
{
    private const int MaxEntriesLimit = 1024;
    private const int MaxPinnedBudgetMegabytes = 4096;
    private const int MaxColdLoadConcurrency = 64;
    private const int MaxPendingLoadsLimit = 1024;

    private static readonly TimeSpan MinDuration = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxInternalWait = TimeSpan.FromSeconds(30);

    public ValidateOptionsResult Validate(string? name, CatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> failures =
            [.. MemoryFailures(options), .. RateFailures(options), .. WaitFailures(options)];
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> MemoryFailures(CatalogOptions options)
    {
        if (options.MaxEntries is < 1 or > MaxEntriesLimit)
        {
            yield return "MaxEntries must lie between 1 and 1024.";
        }

        if (options.VersionsLifetime < MinDuration || options.VersionsLifetime > MaxLifetime)
        {
            yield return "VersionsLifetime must lie between 1 ms and one hour.";
        }

        if (PinnedLanguageList.Parse(options.PinnedLanguages) is null)
        {
            yield return "PinnedLanguages must list Data Dragon languages such as en_US.";
        }

        if (options.PinnedBudgetMegabytes is < 0 or > MaxPinnedBudgetMegabytes)
        {
            yield return "PinnedBudgetMegabytes must lie between 0 and 4096.";
        }
    }

    private static IEnumerable<string> RateFailures(CatalogOptions options)
    {
        // Each caller keeps at least one load a second: the renderer's share is the rest.
        if (options.PublicColdLoadsPerSecond < 1
            || options.PublicColdLoadsPerSecond >= options.ColdLoadsPerSecond)
        {
            yield return "PublicColdLoadsPerSecond must lie between 1 and ColdLoadsPerSecond - 1.";
        }

        if (options.ColdLoadBurst < options.ColdLoadsPerSecond)
        {
            yield return "ColdLoadBurst must be at least ColdLoadsPerSecond.";
        }
    }

    private static IEnumerable<string> WaitFailures(CatalogOptions options)
    {
        if (options.ColdLoadConcurrency is < 1 or > MaxColdLoadConcurrency)
        {
            yield return "ColdLoadConcurrency must lie between 1 and 64.";
        }

        if (options.InternalColdLoadWait < MinDuration
            || options.InternalColdLoadWait > MaxInternalWait)
        {
            yield return "InternalColdLoadWait must lie between 1 ms and 30 seconds.";
        }

        if (options.MaxPendingLoads is < 0 or > MaxPendingLoadsLimit)
        {
            yield return "MaxPendingLoads must lie between 0 and 1024.";
        }
    }
}
