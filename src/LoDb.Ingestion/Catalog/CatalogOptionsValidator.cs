using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog;

/// <summary>
/// Refuses at startup a catalog that could hold nothing, or versions cached for too long.
/// </summary>
internal sealed class CatalogOptionsValidator : IValidateOptions<CatalogOptions>
{
    private const int MaxEntriesLimit = 1024;

    private static readonly TimeSpan MinLifetime = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(1);

    public ValidateOptionsResult Validate(string? name, CatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.MaxEntries is < 1 or > MaxEntriesLimit)
        {
            failures.Add("MaxEntries must lie between 1 and 1024.");
        }

        if (options.VersionsLifetime < MinLifetime || options.VersionsLifetime > MaxLifetime)
        {
            failures.Add("VersionsLifetime must lie between 1 ms and one hour.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
