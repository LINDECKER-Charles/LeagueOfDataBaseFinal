using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;

namespace LoDb.Ingestion.Images;

/// <summary>
/// An image a dataset references: its kind and its file name as the dataset writes it
/// ("Ahri.png"), or its icon path for a rune ("perk-images/Styles/7200_Domination.png").
/// </summary>
/// <remarks>
/// The manifest row of an image is (version, <see cref="ManifestType"/>, <see cref="File"/>);
/// the kind only decides the URL it is fetched from.
/// </remarks>
public sealed record DdragonImage
{
    /// <summary>Longest key the manifest stores (<c>ddragon_asset.key</c>).</summary>
    public const int MaxFileLength = 255;

    public DdragonImage(DdragonImageKind kind, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(file.Length, MaxFileLength);
        Kind = kind;
        File = file;
    }

    public DdragonImageKind Kind { get; }

    /// <summary>The manifest key: file name, or icon path for a rune.</summary>
    public string File { get; }

    /// <summary>One of <see cref="ManifestTypes"/>.</summary>
    public string ManifestType => Kind switch
    {
        DdragonImageKind.Champion or DdragonImageKind.Passive or DdragonImageKind.ChampionSpell
            => ManifestTypes.Champion,
        DdragonImageKind.Item => ManifestTypes.Item,
        DdragonImageKind.SummonerSpell => ManifestTypes.Summoner,
        DdragonImageKind.Rune => ManifestTypes.Runes,
        _ => throw new InvalidOperationException($"Unknown image kind {Kind}."),
    };

    /// <summary>Path relative to Data Dragon's CDN root (<c>DdragonImagePath</c>).</summary>
    public string RelativePath(PatchVersion version) => Kind switch
    {
        DdragonImageKind.Champion => DdragonImagePath.Champion(version, File),
        DdragonImageKind.Passive => DdragonImagePath.Passive(version, File),
        DdragonImageKind.ChampionSpell or DdragonImageKind.SummonerSpell
            => DdragonImagePath.Spell(version, File),
        DdragonImageKind.Item => DdragonImagePath.Item(version, File),
        DdragonImageKind.Rune => DdragonImagePath.RuneIcon(File),
        _ => throw new InvalidOperationException($"Unknown image kind {Kind}."),
    };

    /// <summary>The URL the ingestion fetches the image from.</summary>
    public Uri Url(PatchVersion version) => DdragonUrls.Image(RelativePath(version));
}
