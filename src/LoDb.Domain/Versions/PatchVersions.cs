namespace LoDb.Domain.Versions;

/// <summary>
/// The version list of Data Dragon's <c>versions.json</c>, as the site offers it.
/// </summary>
/// <remarks>
/// Riot still lists pre-2013 <c>lolpatch_*</c> entries that carry no modern dataset (UP 4):
/// every entry that is not a well-formed version is dropped. The list is sorted newest first
/// instead of trusting the upstream order, so "the first entry is the latest" holds by
/// construction.
/// </remarks>
public static class PatchVersions
{
    public static IReadOnlyList<PatchVersion> Normalize(IEnumerable<string?> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var versions = new HashSet<PatchVersion>();
        foreach (var entry in entries)
        {
            if (PatchVersion.TryParse(entry, out var version))
            {
                versions.Add(version);
            }
        }

        return [.. versions.OrderDescending()];
    }
}
