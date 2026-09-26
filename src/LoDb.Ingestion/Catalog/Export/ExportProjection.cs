using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.Derived.Stats;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Catalog.Export;

/// <summary>The JSON of <see cref="CatalogExport"/>, built once its images are resolved.</summary>
internal sealed class ExportProjection(CatalogSnapshot catalog, ImageResolution images)
{
    public JsonObject Build() => new()
    {
        ["version"] = catalog.Version.Value,
        ["language"] = catalog.Language.Code,
        ["champions"] = Resource(catalog.Champions, Champion),
        ["items"] = Resource(catalog.Items, Item),
        ["runes"] = Resource(catalog.Runes, Tree),
        ["summoners"] = Resource(catalog.Summoners, Summoner),
    };

    private static JsonObject Resource<TEntry>(
        CatalogResource<TEntry> resource,
        Func<TEntry, JsonNode> project)
        where TEntry : class => new()
    {
        ["contentLanguage"] = resource.ContentLanguage?.Code,
        ["entries"] = ArrayOf(resource.Entries, project),
    };

    private static JsonObject Skin(Skin skin) => new()
    {
        ["id"] = skin.Id,
        ["number"] = skin.Number,
        ["name"] = skin.Name,
        ["chromas"] = ArrayOf(skin.Chromas, static chroma => new JsonObject
        {
            ["id"] = chroma.Id,
            ["name"] = chroma.Name,
            ["label"] = ChromaLabel.Of(chroma),
        }),
    };

    private static JsonObject? Twin(EditionTwin? twin) => twin is null
        ? null
        : new JsonObject { ["id"] = twin.Id, ["edition"] = Token(twin.Edition) };

    private static JsonObject Stat(ItemStat stat) => new()
    {
        ["stat"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(stat.Stat.ToString()),
        ["value"] = stat.Value,
        ["percent"] = stat.IsPercent,
    };

    private static string Token<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string? OptionalToken<TEnum>(TEnum? value)
        where TEnum : struct, Enum =>
        value is { } present ? Token(present) : null;

    private static JsonArray ArrayOf<T>(IEnumerable<T> values, Func<T, JsonNode?> project) =>
        new(values.Select(project).ToArray());

    private JsonObject Champion(ChampionDetail champion) => new()
    {
        ["id"] = champion.Summary.Id,
        ["key"] = champion.Summary.Key,
        ["name"] = champion.Summary.Name,
        ["title"] = champion.Summary.Title,
        ["path"] = catalog.PathOf(champion).Value,
        ["resource"] = catalog.ResourceTokenOf(champion),
        ["attackRange"] = OptionalToken(AttackRange.ClassOf(champion.Summary)),
        ["image"] = Image(DdragonImageKind.Champion, champion.Summary.Image),
        ["passive"] = champion.Passive is { } passive
            ? new JsonObject
            {
                ["name"] = passive.Name,
                ["image"] = Image(DdragonImageKind.Passive, passive.Image),
            }
            : null,
        ["spells"] = ArrayOf(champion.Spells, spell => new JsonObject
        {
            ["id"] = spell.Id,
            ["name"] = spell.Name,
            ["image"] = Image(DdragonImageKind.ChampionSpell, spell.Image),
        }),
        ["skins"] = ArrayOf(champion.Skins, Skin),
    };

    private JsonObject Item(Item item) => new()
    {
        ["id"] = item.Id,
        ["name"] = item.Name,
        ["path"] = catalog.PathOf(item).Value,
        ["edition"] = Token(item.Edition),
        ["counterpart"] = Twin(item.Counterpart),
        ["listed"] = catalog.IsListed(item),
        ["tier"] = OptionalToken(ItemTiers.Of(item)),
        ["stats"] = ArrayOf(ItemStats.Of(item.Stats), Stat),
        ["image"] = Image(DdragonImageKind.Item, item.Image),
    };

    private JsonObject Tree(RuneTree tree) => new()
    {
        ["id"] = tree.Id,
        ["key"] = tree.Key,
        ["name"] = tree.Name,
        ["path"] = catalog.PathOf(tree).Value,
        ["image"] = Image(DdragonImageKind.Rune, tree.Icon),
        ["slots"] = ArrayOf(tree.Slots, slot => ArrayOf(slot.Runes, Rune)),
    };

    private JsonObject Rune(Rune rune) => new()
    {
        ["id"] = rune.Id,
        ["key"] = rune.Key,
        ["name"] = rune.Name,
        ["image"] = Image(DdragonImageKind.Rune, rune.Icon),
    };

    private JsonObject Summoner(SummonerSpell spell) => new()
    {
        ["id"] = spell.Id,
        ["key"] = spell.Key,
        ["name"] = spell.Name,
        ["path"] = catalog.PathOf(spell).Value,
        ["edition"] = Token(spell.Edition),
        ["counterpart"] = Twin(spell.Counterpart),
        ["image"] = Image(DdragonImageKind.SummonerSpell, spell.Image),
    };

    // Null for a file name the manifest cannot hold, which no ingestion asks for.
    private JsonObject? Image(DdragonImageKind kind, string? file)
    {
        if (VersionImages.ImageOf(kind, file) is not { } image)
        {
            return null;
        }

        var resolved = images[image];
        return new JsonObject
        {
            ["file"] = image.File,
            ["status"] = Token(resolved.Status),
            ["url"] = resolved.Url,
        };
    }
}
