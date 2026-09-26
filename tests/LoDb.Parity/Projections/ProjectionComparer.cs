using LoDb.Parity.Deviations;

namespace LoDb.Parity.Projections;

/// <summary>
/// Compares the legacy and the new projection of one (version, language), resource by
/// resource: content language, entries by id, entry order, then every projected field.
/// </summary>
public static class ProjectionComparer
{
    public static IReadOnlyList<string> Resources { get; } =
        ["champions", "items", "runes", "summoners"];

    public static IReadOnlyList<Deviation> Compare(ProjectionPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return
        [
            .. Resources.SelectMany(resource =>
            {
                var site = new DeviationSite
                {
                    Version = pair.Version,
                    Language = pair.Language,
                    Resource = resource,
                };
                return new ResourceComparison(site, pair.Legacy[resource], pair.Next[resource])
                    .Deviations();
            }),
        ];
    }
}
