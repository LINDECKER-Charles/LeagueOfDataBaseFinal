using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>What the database knows of the page store, and the epochs that frame it.</summary>
internal sealed record StorageFacts
{
    public required IReadOnlyList<StoredUnit> Units { get; init; }

    public required StoredBuilds Builds { get; init; }

    /// <summary>The epoch nginx serves, from the settings.</summary>
    public required int ServedEpoch { get; init; }

    /// <summary>The epoch the active pass writes; null while none is active.</summary>
    public int? WritingEpoch { get; init; }

    /// <summary>
    /// The coverage of each epoch not retired: the served one, the one written and every one
    /// awaiting the switch, even without a page yet.
    /// </summary>
    public IReadOnlyDictionary<int, PrerenderEpochCoverage> Coverages { get; init; } =
        new Dictionary<int, PrerenderEpochCoverage>();
}
