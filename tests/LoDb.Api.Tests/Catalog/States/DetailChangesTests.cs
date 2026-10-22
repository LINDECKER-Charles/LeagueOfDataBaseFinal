using System.Text.Json;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// The block « Ce qui a changé » of an opener at 16.18.1 over real pairs of the recording, the
/// index seeded as it is built: with the keys the engine finds between the en_US entries of the
/// entity's previous state, at 8.7.1, and of its page. Fiddlesticks is compared to 3.13.24, when
/// Data Dragon wrote its id FiddleSticks. Data Dragon shipped ar_AE after both: their ar_AE
/// catalogs hold en_US.
/// </summary>
[Collection(StateApiGroup.Name)]
public sealed class DetailChangesTests(StateApiFixture api) : IDisposable
{
    private const string Ancient = "3.13.24";
    private const string Archived = "8.7.1";
    private const string Opener = "16.18.1";
    private const string Latest = "16.19.1";
    private const string English = "en_US";
    private const string Arabic = "ar_AE";

    private static readonly IndexedEntity Fiddlesticks =
        new(ResourceType.Champions, "Fiddlesticks");

    private static readonly IndexedEntity TrinityForce = new(ResourceType.Items, "3078");

    // The block, the history and the page of the patch list the same fields, in any language.
    [Theory]
    [InlineData(ResourceType.Items, "3078", "fr_FR")]
    [InlineData(ResourceType.Items, "3078", "zh_CN")]
    [InlineData(ResourceType.Champions, "Fiddlesticks", "fr_FR")]
    [InlineData(ResourceType.Champions, "Fiddlesticks", "zh_CN")]
    [InlineData(ResourceType.Runes, "8000", "fr_FR")]
    [InlineData(ResourceType.Summoners, "SummonerFlash", "fr_FR")]
    [InlineData(ResourceType.Items, "3078", Arabic)]
    [InlineData(ResourceType.Champions, "Fiddlesticks", Arabic)]
    public async Task BlockListsTheFieldsTheIndexMarksChanged(
        ResourceType resource,
        string id,
        string language)
    {
        var entity = new IndexedEntity(resource, id);
        var keys = await SeedAsync(entity);

        var page = await api.GetJsonAsync(PathOf(entity, language));

        Assert.Equal(keys, KeysOf(page.GetProperty("changes").Items("fields")));
        Assert.Equal(keys, KeysOf(page.GetProperty("state").Items("changedFields")));
    }

    // Abyssal Mask's id went to Evenshroud: each side shows the values it has.
    [Fact]
    public async Task EachSideShowsItsValuesInThePageLanguage()
    {
        var fields = await FieldsAsync(new IndexedEntity(ResourceType.Items, "3001"), "fr_FR");

        Assert.Equal(["Masque abyssal", "Linceul de la congruence"], Sides(fields["name"]));
        Assert.Equal(["true", "false"], Sides(fields["purchasable"]));
        Assert.Empty(fields["stat.armor"].Items("before"));
        Assert.Equal(["30"], fields["stat.armor"].Pluck("after", "value"));
        Assert.Equal(["300"], fields["stat.mana"].Pluck("before", "value"));
        Assert.Empty(fields["stat.mana"].Items("after"));
    }

    // Berserker's Greaves: each component of the recipe is named, once per occurrence.
    [Fact]
    public async Task RecipeNamesItsComponents()
    {
        var fields = await FieldsAsync(new IndexedEntity(ResourceType.Items, "3006"), "fr_FR");

        Assert.Equal(["Bottes", "Dague"], fields["recipe"].Pluck("before", "label"));
        Assert.Equal(["Bottes", "Dague", "Dague"], fields["recipe"].Pluck("after", "label"));
    }

    [Fact]
    public async Task ChampionIsComparedToItsEntryWhateverTheCaseOfItsIdThen()
    {
        await SeedAsync(Fiddlesticks);

        var page = await api.GetJsonAsync(PathOf(Fiddlesticks, "fr_FR"));
        var fields = FieldsOf(page);
        var cooldown = fields["spellCooldown.e"];

        Assert.Equal(Ancient, page.GetProperty("changes").Text("comparedVersion"));
        Assert.Equal("Fauchaison", cooldown.GetProperty("subject").Text("name"));
        Assert.Equal(["15/14/13/12/11", "10/9/8/7/6"], Sides(cooldown));
        Assert.Equal(["Vent des ténèbres", "Fauchaison"], Sides(fields["spellName.e"]));
        Assert.Contains("Fiddlesticks réanimé", fields["skins"].Pluck("after", "label"));
    }

    // Its title changed in en_US, not in zh_CN.
    [Fact]
    public async Task ValueEqualInThePageLanguageShowsItsValueAfterAlone()
    {
        var fields = await FieldsAsync(Fiddlesticks, "zh_CN");

        Assert.Empty(fields["title"].Items("before"));
        Assert.Equal(["费德提克"], fields["title"].Pluck("after", "value"));
    }

    // Trinity Force's redesign, split into words or, in zh_CN, into characters.
    [Theory]
    [InlineData("fr_FR")]
    [InlineData("zh_CN")]
    public async Task TextTravelsBySegmentsThatRebuildEachSide(string language)
    {
        var description = (await FieldsAsync(TrinityForce, language))["description"];
        var segments = description.Items("segments");

        Assert.Empty(description.Items("before"));
        Assert.Empty(description.Items("after"));
        Assert.Equal(
            await DescriptionAsync(Archived, language),
            Rebuilt(segments, "removed"));
        Assert.Equal(
            await DescriptionAsync(Opener, language),
            Rebuilt(segments, "added"));
    }

    // Fiddlesticks has had no tips since its rework.
    [Fact]
    public async Task TextGoneShowsRemovedWhole()
    {
        var tips = (await FieldsAsync(Fiddlesticks, "fr_FR"))["allyTips"];

        Assert.Equal(["removed"], tips.Pluck("segments", "kind"));
    }

    // The index keys en_US fields: one that neither side has in the page's language is listed
    // all the same, without values.
    [Fact]
    public async Task FieldNeitherSideHasIsListedWithoutValues()
    {
        await SeedAsync(TrinityForce, FieldKey.Of(ChangeField.RequiredChampion));

        var page = await api.GetJsonAsync(PathOf(TrinityForce, "fr_FR"));
        var required = FieldsOf(page)["requiredChampion"];

        Assert.Equal(
            KeysOf(page.GetProperty("state").Items("changedFields")),
            KeysOf(page.GetProperty("changes").Items("fields")));
        Assert.Empty(Sides(required));
        Assert.Empty(required.Items("segments"));
    }

    // Set against en_US, a text reads as a translation that did not change; a number does not
    // depend on the language.
    [Fact]
    public async Task TextComparedToEnglishShowsAfterAlone()
    {
        var fields = await FieldsAsync(TrinityForce, Arabic);
        var description = fields["description"];

        Assert.Equal(["same"], description.Pluck("segments", "kind"));
        Assert.Equal(
            [await DescriptionAsync(Opener, Arabic)],
            description.Pluck("segments", "text"));
        Assert.Equal(["3733", "3333"], Sides(fields["goldTotal"]));
    }

    // Its spells' per-rank strings read the same in every language; its tips, English only
    // before, are listed without values.
    [Fact]
    public async Task ChampionComparedToEnglishKeepsWhatEveryLanguageWritesAlike()
    {
        var fields = await FieldsAsync(Fiddlesticks, Arabic);
        var champion = (await api.CatalogAsync(Opener, Arabic)).FindChampion(Fiddlesticks.Id);
        Assert.NotNull(champion);
        var reap = champion.Spells[2].Name;

        Assert.Equal(["15/14/13/12/11", "10/9/8/7/6"], Sides(fields["spellCooldown.e"]));
        Assert.Equal([reap], Sides(fields["spellName.e"]));
        Assert.Equal(reap, fields["spellName.e"].GetProperty("subject").Text("name"));
        Assert.Empty(fields["allyTips"].Items("segments"));
        Assert.Empty(Sides(fields["allyTips"]));
    }

    public void Dispose() => api.Reset();

    private static string PathOf(IndexedEntity entity, string language) =>
        $"/api/catalog/{Opener}/{language}/{CanonicalPath.SegmentOf(entity.Resource)}/{entity.Id}";

    private static string ComparedOf(IndexedEntity entity) =>
        entity == Fiddlesticks ? Ancient : Archived;

    private static string KeyOf(JsonElement field) =>
        field.IsNull("subject")
            ? field.Text("field")
            : $"{field.Text("field")}.{field.GetProperty("subject").Text("key")}";

    // A field as the API names it, its subject after a dot.
    private static string NameOf(FieldKey key)
    {
        var field = JsonNamingPolicy.CamelCase.ConvertName(key.Field.ToString());
        return key.Subject is { } subject ? $"{field}.{subject}" : field;
    }

    private static IReadOnlyList<string> KeysOf(IReadOnlyList<JsonElement> fields) =>
        [.. fields.Select(KeyOf)];

    private static Dictionary<string, JsonElement> FieldsOf(JsonElement page) =>
        page.GetProperty("changes").Items("fields").ToDictionary(KeyOf, StringComparer.Ordinal);

    // The values before, then after.
    private static IReadOnlyList<string> Sides(JsonElement field) =>
        [.. field.Pluck("before", "value"), .. field.Pluck("after", "value")];

    // The text of one side: what both share, and what that side alone has.
    private static string Rebuilt(IReadOnlyList<JsonElement> segments, string side) =>
        string.Concat(segments
            .Where(segment => segment.Text("kind") is "same" || segment.Text("kind") == side)
            .Select(static segment => segment.Text("text")));

    // The index the page reads: the entity changed at 16.18.1, its keys the engine's in en_US,
    // then those a test adds.
    private async Task<IReadOnlyList<string>> SeedAsync(
        IndexedEntity entity,
        params FieldKey[] added)
    {
        var compared = ComparedOf(entity);
        var keys = EntityDiff.ChangedKeys(
            ListedProjections.Of(await api.CatalogAsync(compared, English), entity),
            ListedProjections.Of(await api.CatalogAsync(Opener, English), entity));
        api.Seed(
            [compared, Opener, Latest],
            [
                TestStates.Closed(entity, compared, compared),
                TestStates.Open(entity, Opener) with
                {
                    PreviousOpener = PatchVersion.Parse(compared),
                    ChangedFields = [.. keys, .. added],
                },
            ]);
        return [.. keys.Concat(added).Select(NameOf)];
    }

    private async Task<Dictionary<string, JsonElement>> FieldsAsync(
        IndexedEntity entity,
        string language)
    {
        await SeedAsync(entity);
        return FieldsOf(await api.GetJsonAsync(PathOf(entity, language)));
    }

    // Trinity Force's description as the engine reads it.
    private async Task<string> DescriptionAsync(string version, string language)
    {
        var catalog = await api.CatalogAsync(version, language);
        return ListedProjections.Of(catalog, TrinityForce)
            .Single(static field => field.Key == FieldKey.Of(ChangeField.Description))
            .Values[0];
    }
}
