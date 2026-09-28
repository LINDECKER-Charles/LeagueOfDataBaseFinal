using LoDb.Domain.Catalog.Champions;

namespace LoDb.Ingestion.Normalization;

/// <summary>
/// CommunityDragon's chromas of one version, by Data Dragon skin id (UP 9).
/// </summary>
/// <remarks>
/// Read once per version and shared by every language: chromas carry no translated text
/// that the site shows.
/// </remarks>
public sealed class ChromaCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Chroma>> bySkin;

    internal ChromaCatalog(string? patch, IReadOnlyDictionary<string, IReadOnlyList<Chroma>> bySkin)
    {
        Patch = patch;
        this.bySkin = bySkin;
    }

    /// <summary>No chroma: CommunityDragon has neither the patch nor <c>latest</c>.</summary>
    public static ChromaCatalog Empty { get; } =
        new(null, new Dictionary<string, IReadOnlyList<Chroma>>(StringComparer.Ordinal));

    /// <summary>
    /// The CommunityDragon patch that answered ("16.19", or "latest" after a fallback),
    /// <see langword="null"/> when none did.
    /// </summary>
    public string? Patch { get; }

    /// <summary>Number of skins that have chromas.</summary>
    public int Count => bySkin.Count;

    /// <summary>The chromas of a skin; none for an unknown or empty id.</summary>
    public IReadOnlyList<Chroma> For(string skinId)
    {
        ArgumentNullException.ThrowIfNull(skinId);
        return bySkin.TryGetValue(skinId, out var chromas) ? chromas : [];
    }
}
