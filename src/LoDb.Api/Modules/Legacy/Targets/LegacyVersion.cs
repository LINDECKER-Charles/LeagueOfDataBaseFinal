using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Legacy.Targets;

/// <summary>The version of an old catalog URL, as the new one writes and reads it.</summary>
internal sealed record LegacyVersion
{
    /// <summary>
    /// The version a pinned target names, for an entry gone from the latest patch or the list
    /// standing in for it; <see langword="null"/> for the latest one, whose URL is the short
    /// form, so that no second redirect follows.
    /// </summary>
    public PatchVersion? Pinned { get; init; }

    /// <summary>
    /// The version whose catalog knows the old names: the pinned one, else the latest;
    /// <see langword="null"/> before the first promotion.
    /// </summary>
    public PatchVersion? Catalog { get; init; }
}
