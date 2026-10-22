using LoDb.Api.Modules.Catalog.States.Details;
using LoDb.Domain.Catalog;
using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// A field the diff engine compared, as the API writes it, over the recorded catalogs: what a
/// value or a subject designates by id is named in the page's language, by the catalog
/// compared to when the page's lacks it.
/// </summary>
[Collection(StateApiGroup.Name)]
public sealed class FieldChangesTests(StateApiFixture api)
{
    private static readonly IndexedEntity Garen = new(ResourceType.Champions, "Garen");
    private static readonly IndexedEntity TrinityForce = new(ResourceType.Items, "3078");
    private static readonly IndexedEntity Precision = new(ResourceType.Runes, "8000");

    [Fact]
    public async Task EntitiesOfAValueAreNamedInThePageLanguage()
    {
        var names = new EntityNames(TrinityForce, await api.CatalogAsync("16.19.1", "fr_FR"), null);

        var recipe = FieldChanges.Of(
            Diff(FieldKey.Of(ChangeField.Recipe), ["1042"], ["1042", "1001"]),
            names);

        Assert.Equal((FieldKind.List, ValueKind.Entity), (recipe.Kind, recipe.ValueKind));
        Assert.Equal(["Dague"], recipe.Before.Select(static value => value.Label));
        Assert.Equal(["1042", "1001"], recipe.After.Select(static value => value.Value));
        Assert.Equal(["Dague", "Bottes"], recipe.After.Select(static value => value.Label));
        Assert.Empty(recipe.Segments);
    }

    // Molten Edge left the game after 8.7.1: only the catalog compared names it.
    [Fact]
    public async Task EntityTheVersionAfterLacksIsNamedByTheCatalogCompared()
    {
        var names = new EntityNames(
            TrinityForce,
            await api.CatalogAsync("16.19.1", "en_US"),
            await api.CatalogAsync("8.7.1", "en_US"));

        var recipe = FieldChanges.Of(
            Diff(FieldKey.Of(ChangeField.Recipe), ["3371"], ["1042"]),
            names);

        Assert.Equal("Molten Edge", recipe.Before.Single().Label);
    }

    [Fact]
    public async Task ChampionNamesItsSkinsAndItsSpells()
    {
        var names = new EntityNames(Garen, await api.CatalogAsync("16.19.1", "fr_FR"), null);

        var skins = FieldChanges.Of(
            Diff(FieldKey.Of(ChangeField.Skins), ["86000"], ["86000", "86001"]),
            names);
        var cooldown = FieldChanges.Of(
            Diff(FieldKey.Of(ChangeField.SpellCooldown, "q"), ["8"], ["7"]),
            names);

        Assert.Equal(["Garen", "Garen sanguin"], skins.After.Select(static value => value.Label));
        Assert.Equal(SubjectKind.SpellSlot, cooldown.Subject?.Kind);
        Assert.Equal(("q", "Coup décisif"), (cooldown.Subject?.Key, cooldown.Subject?.Name));
        Assert.Null(cooldown.After.Single().Label);
    }

    [Fact]
    public async Task RunesOfARowAreNamedAndAnUnknownSubjectKeepsTheEngineName()
    {
        var names = new EntityNames(Precision, await api.CatalogAsync("16.19.1", "en_US"), null);

        var keystones = FieldChanges.Of(
            Diff(FieldKey.Of(ChangeField.RuneSlot, "keystone"), ["8005"], ["8008"]),
            names);
        var unknown = FieldChanges.Of(
            Diff(FieldKey.Of(ChangeField.RuneName, "9999"), ["Old"], ["New"]) with
            {
                SubjectName = "New",
            },
            names);

        Assert.Equal("Press the Attack", keystones.Before.Single().Label);
        Assert.Null(keystones.Subject?.Name);
        Assert.Equal("New", unknown.Subject?.Name);
    }

    private static FieldDiff Diff(FieldKey key, string[] before, string[] after) => new()
    {
        Key = key,
        Before = before,
        After = after,
        Segments = [],
    };
}
