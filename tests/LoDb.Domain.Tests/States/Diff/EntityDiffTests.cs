using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Languages;
using LoDb.Domain.States.Diff;
using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.States.Diff;

/// <summary>
/// The block « Ce qui a changé »: only the keys the index marks changed, with the values of
/// the page's language, and nothing for a new entity.
/// </summary>
public sealed class EntityDiffTests
{
    private static readonly DdragonLanguage French = DdragonLanguage.Parse("fr_FR");

    [Fact]
    public void ANewEntityHasNoChange()
    {
        var after = ItemProjection.Of(ItemSamples.Named("1036", "Épée longue"));

        Assert.Empty(EntityDiff.Compare(null, after, Scope("name")));
    }

    [Fact]
    public void OnlyTheKeysOfTheIndexAreCompared()
    {
        var before = ItemSamples.Named("1036", "Épée longue") with
        {
            Gold = ItemSamples.Gold(total: 350, combine: 350),
        };
        var after = before with
        {
            Name = "Longue épée",
            Gold = ItemSamples.Gold(total: 400, combine: 400),
        };

        var changes = EntityDiff.Compare(
            ItemProjection.Of(before),
            ItemProjection.Of(after),
            Scope("gold.total"));

        var change = Assert.Single(changes);
        Assert.Equal(("gold.total", "350", "400"), Values(change));
        Assert.Empty(change.Segments);
    }

    [Fact]
    public void AFieldEqualInThePageLanguageShowsItsValueAfterAlone()
    {
        var item = ItemSamples.Named("1036", "Épée longue");

        var change = Assert.Single(EntityDiff.Compare(
            ItemProjection.Of(item),
            ItemProjection.Of(item),
            Scope("name")));

        Assert.Empty(change.Before);
        Assert.Equal(["Épée longue"], change.After);
    }

    [Fact]
    public void AFieldGoneHasNoValueAfter()
    {
        var before = ItemSamples.Named("3006", "Jambières") with { Depth = 2 };

        var change = Assert.Single(EntityDiff.Compare(
            ItemProjection.Of(before),
            ItemProjection.Of(before with { Depth = null }),
            Scope("depth")));

        Assert.Equal(("depth", "2", string.Empty), Values(change));
    }

    [Fact]
    public void ATextTravelsAsSegmentsAndASpellIsNamed()
    {
        var before = ChampionSamples.Spell("AhriQ", "7") with
        {
            Name = "Orbe d'illusion",
            Description = "Lance son <b>orbe</b>.",
        };
        var after = before with { Description = "Lance son orbe deux fois." };

        var change = Assert.Single(EntityDiff.Compare(
            ChampionProjection.Of(Ahri(before)),
            ChampionProjection.Of(Ahri(after)),
            Scope("spell.q.description")));

        Assert.Equal("Orbe d'illusion", change.SubjectName);
        Assert.Empty(change.Before);
        Assert.Empty(change.After);
        Assert.Equal(
            [
                new TextSegment { Kind = SegmentKind.Same, Text = "Lance son orbe" },
                new TextSegment { Kind = SegmentKind.Added, Text = " deux fois" },
                new TextSegment { Kind = SegmentKind.Same, Text = "." },
            ],
            change.Segments);
    }

    private static ChampionDetail Ahri(ChampionSpell spell) =>
        ChampionSamples.Detail(ChampionSamples.Named("Ahri", "Ahri"), [spell], []);

    private static DiffScope Scope(params string[] keys) => new()
    {
        Language = French,
        Keys = keys.Select(FieldKey.Parse).ToHashSet(),
    };

    private static (string Key, string Before, string After) Values(FieldDiff change) =>
        (change.Key.ToDotted(), string.Join('|', change.Before), string.Join('|', change.After));
}
