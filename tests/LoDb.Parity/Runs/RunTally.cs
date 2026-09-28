using System.Text.Json.Nodes;
using LoDb.Parity.Manifests;
using LoDb.Parity.Projections;

namespace LoDb.Parity.Runs;

/// <summary>
/// What a run compared, so that a report without deviations says over how much: entries
/// per resource, champion details, manifest keys per type.
/// </summary>
public sealed class RunTally
{
    private readonly Dictionary<string, (int Legacy, int Next)> entries = [];
    private readonly Dictionary<string, (int Legacy, int Next, int Shared)> keys = [];

    /// <summary>Entries per resource, summed over every pair.</summary>
    public IReadOnlyDictionary<string, (int Legacy, int Next)> Entries => entries;

    /// <summary>Champions compared with their detail (passive, spells, skins, chromas).</summary>
    public int DetailedChampions { get; private set; }

    /// <summary>Manifest keys per type, summed over every version.</summary>
    public IReadOnlyDictionary<string, (int Legacy, int Next, int Shared)> Keys => keys;

    public void Count(ProjectionPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        foreach (var resource in ProjectionComparer.Resources)
        {
            var legacy = EntriesOf(pair.Legacy, resource);
            var next = EntriesOf(pair.Next, resource);
            var (legacyTotal, nextTotal) = entries.GetValueOrDefault(resource);
            entries[resource] = (legacyTotal + legacy.Count, nextTotal + next.Count);
        }

        DetailedChampions += EntriesOf(pair.Legacy, "champions")
            .Count(static entry => entry?["detail"]?.GetValue<bool>() == true);
    }

    public void Count(ManifestPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var shared = pair.Legacy.Keys.Count(pair.Next.ContainsKey);
        var (legacy, next, both) = keys.GetValueOrDefault(pair.Type);
        keys[pair.Type] = (legacy + pair.Legacy.Count, next + pair.Next.Count, both + shared);
    }

    private static JsonArray EntriesOf(JsonObject projection, string resource) =>
        projection[resource]?["entries"] as JsonArray ?? [];
}
