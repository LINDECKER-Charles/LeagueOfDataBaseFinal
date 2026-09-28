using System.Collections.Frozen;
using LoDb.Domain.Catalog;

namespace LoDb.Domain.Paths;

/// <summary>
/// Canonical path of an entity page, below the locale and version prefixes:
/// <c>champions/{id}</c>, <c>items/{id}-{slug}</c>, <c>runes/{id}-{slug}</c>,
/// <c>summoners/{id}</c>.
/// </summary>
/// <remarks>
/// The one source of this grammar: the API returns it, and sitemaps, legacy redirects,
/// analytics and the front use it as given. The id identifies (classic twins share their
/// name); the slug, derived from the en_US name, is decorative and the same in every locale.
/// A request with a wrong or missing slug is redirected to <see cref="Value"/>.
/// </remarks>
public sealed record CanonicalPath
{
    private const char SegmentSeparator = '/';
    private const char SlugSeparator = '-';

    private static readonly FrozenDictionary<ResourceType, string> Segments =
        new Dictionary<ResourceType, string>
        {
            [ResourceType.Champions] = "champions",
            [ResourceType.Items] = "items",
            [ResourceType.Runes] = "runes",
            [ResourceType.Summoners] = "summoners",
        }.ToFrozenDictionary();

    private CanonicalPath(ResourceType type, string id, string slug)
    {
        Type = type;
        Id = id;
        Slug = slug;
    }

    public ResourceType Type { get; }

    public string Id { get; }

    /// <summary>Slug of the path; empty for the resources addressed by id alone.</summary>
    public string Slug { get; }

    /// <summary>The path itself ("items/1036-long-sword").</summary>
    public string Value => Slug.Length == 0
        ? $"{Segments[Type]}{SegmentSeparator}{Id}"
        : $"{Segments[Type]}{SegmentSeparator}{Id}{SlugSeparator}{Slug}";

    /// <param name="id">Champion id as Data Dragon keys it ("MonkeyKing").</param>
    public static CanonicalPath Champion(string id) =>
        new(ResourceType.Champions, EntityIds.RequireWord(id), string.Empty);

    /// <param name="id">Numeric item id ("1036", "771036").</param>
    /// <param name="englishName">en_US display name of the item.</param>
    public static CanonicalPath Item(string id, string? englishName) =>
        new(ResourceType.Items, EntityIds.RequireNumber(id), Slugs.From(englishName));

    /// <param name="id">Rune path id (8000).</param>
    /// <param name="englishName">en_US name of the rune path.</param>
    public static CanonicalPath RuneTree(int id, string? englishName) =>
        new(ResourceType.Runes, EntityIds.RequireNumber(id), Slugs.From(englishName));

    /// <param name="id">Summoner spell id ("SummonerFlash_Jade").</param>
    public static CanonicalPath Summoner(string id) =>
        new(ResourceType.Summoners, EntityIds.RequireWord(id), string.Empty);

    /// <summary>First segment of the resource's paths ("items").</summary>
    public static string SegmentOf(ResourceType type) => Segments[type];

    /// <summary>
    /// The id a requested path segment designates, whatever its slug says, or
    /// <see langword="null"/> when the segment cannot name an entity of the resource.
    /// </summary>
    public static string? IdOf(ResourceType type, string segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (type is ResourceType.Champions or ResourceType.Summoners)
        {
            return EntityIds.IsWord(segment) ? segment : null;
        }

        var separator = segment.IndexOf(SlugSeparator, StringComparison.Ordinal);
        var id = separator < 0 ? segment : segment[..separator];
        return EntityIds.IsNumber(id) ? id : null;
    }

    public override string ToString() => Value;
}
