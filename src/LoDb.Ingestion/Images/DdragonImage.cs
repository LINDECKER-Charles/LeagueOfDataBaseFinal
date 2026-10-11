using System.Globalization;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;

namespace LoDb.Ingestion.Images;

/// <summary>
/// An image a dataset references: its kind and its file name as the dataset writes it
/// ("Ahri.png"), or its icon path for a rune ("perk-images/Styles/7200_Domination.png"); or
/// a WebP variant of champion art, at a width (<see cref="ArtVariant"/>).
/// </summary>
/// <remarks>
/// The manifest row of an image is (version, <see cref="ManifestType"/>,
/// <see cref="ManifestKey"/>); the kind only decides the URL it is fetched from.
/// </remarks>
public sealed record DdragonImage
{
    /// <summary>Longest key the manifest stores (<c>ddragon_asset.key</c>).</summary>
    public const int MaxFileLength = 255;

    private const string WidthSeparator = "@";

    public DdragonImage(DdragonImageKind kind, string file)
        : this(kind, file, null)
    {
        if (IsArt(kind))
        {
            throw new ArgumentException("An art variant has a width.", nameof(kind));
        }
    }

    private DdragonImage(DdragonImageKind kind, string file, int? width)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        Kind = kind;
        File = file;
        Width = width;
        ManifestKey = width is { } pixels
            ? string.Concat(file, WidthSeparator, pixels.ToString(CultureInfo.InvariantCulture))
            : file;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            ManifestKey.Length,
            MaxFileLength,
            nameof(file));
    }

    public DdragonImageKind Kind { get; }

    /// <summary>
    /// The file name, or icon path for a rune; for an art variant, the art's path under the
    /// CDN root (<c>img/champion/centered/Ahri_0.jpg</c>).
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The width an art variant is stored at, in pixels, the art never being enlarged;
    /// <see langword="null"/> for an image stored as fetched.
    /// </summary>
    public int? Width { get; }

    /// <summary>
    /// The key of the manifest row: <see cref="File"/>, followed by <c>@{width}</c> for an
    /// art variant (<c>img/champion/centered/Ahri_0.jpg@480</c>).
    /// </summary>
    public string ManifestKey { get; }

    /// <summary>One of <see cref="ManifestTypes"/>.</summary>
    public string ManifestType => Kind switch
    {
        DdragonImageKind.Champion or DdragonImageKind.Passive or DdragonImageKind.ChampionSpell
            => ManifestTypes.Champion,
        DdragonImageKind.Item => ManifestTypes.Item,
        DdragonImageKind.SummonerSpell => ManifestTypes.Summoner,
        DdragonImageKind.Rune => ManifestTypes.Runes,
        DdragonImageKind.CenteredArt or DdragonImageKind.LoadingArt => ManifestTypes.ChampionArt,
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
        DdragonImageKind.CenteredArt or DdragonImageKind.LoadingArt => File,
        _ => throw new InvalidOperationException($"Unknown image kind {Kind}."),
    };

    /// <summary>The URL the ingestion fetches the image from.</summary>
    public Uri Url(PatchVersion version) => DdragonUrls.Image(RelativePath(version));

    /// <summary>
    /// A WebP variant of champion art, <paramref name="width"/> pixels wide at most.
    /// </summary>
    /// <param name="kind"><see cref="DdragonImageKind.CenteredArt"/> or a loading portrait.</param>
    /// <param name="path">The art's path under the CDN root (<c>DdragonImagePath</c>).</param>
    /// <param name="width">The width to store it at, the art never being enlarged.</param>
    public static DdragonImage ArtVariant(DdragonImageKind kind, string path, int width)
    {
        if (!IsArt(kind))
        {
            throw new ArgumentException($"{kind} is not champion art.", nameof(kind));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return new DdragonImage(kind, path, width);
    }

    private static bool IsArt(DdragonImageKind kind) =>
        kind is DdragonImageKind.CenteredArt or DdragonImageKind.LoadingArt;
}
