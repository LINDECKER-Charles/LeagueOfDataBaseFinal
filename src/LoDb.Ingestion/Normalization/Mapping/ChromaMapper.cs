using System.Globalization;
using LoDb.Domain.Catalog.Champions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Ddragon.Raw.CommunityDragon;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// CommunityDragon's <c>skins.json</c> to a <see cref="ChromaCatalog"/> (UP 9).
/// </summary>
internal static class ChromaMapper
{
    /// <param name="patch">The patch that answered, part of each asset path.</param>
    /// <param name="skins">The skins, keyed by skin id.</param>
    public static ChromaCatalog Catalog(
        string patch,
        Dictionary<string, RawCommunityDragonSkin?> skins)
    {
        var bySkin = new Dictionary<string, IReadOnlyList<Chroma>>(StringComparer.Ordinal);
        foreach (var skin in skins.Values)
        {
            if (skin?.Id is not { } skinId)
            {
                continue;
            }

            IReadOnlyList<Chroma> chromas = [.. (skin.Chromas ?? [])
                .Select(chroma => Map(chroma, patch))
                .OfType<Chroma>()];
            if (chromas.Count > 0)
            {
                bySkin[skinId.ToString(CultureInfo.InvariantCulture)] = chromas;
            }
        }

        return new ChromaCatalog(patch, bySkin);
    }

    // The swatch is the whole art of a chroma: without one, there is nothing to show.
    private static Chroma? Map(RawCommunityDragonChroma? chroma, string patch)
    {
        if (chroma?.ChromaPath is not { Length: > 0 } gamePath || chroma.Id is not { } id)
        {
            return null;
        }

        return new Chroma
        {
            Id = id,
            Name = RawValues.Text(chroma.Name),
            Colors = [.. RawValues.Strings(chroma.Colors).Where(static color => color.Length > 0)],
            Image = CommunityDragonUrls.AssetPath(patch, gamePath),
        };
    }
}
