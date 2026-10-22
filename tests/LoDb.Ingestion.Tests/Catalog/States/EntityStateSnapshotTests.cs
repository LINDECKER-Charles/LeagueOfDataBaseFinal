using LoDb.Domain.Catalog;
using LoDb.Domain.States;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog.States;

/// <summary>
/// The queries of a snapshot of the index over five covered versions: Molten Edge changes at
/// 90.3.1 and is retired at 90.5.1, Boots never changes, Garen comes back at 90.4.1.
/// </summary>
public sealed class EntityStateSnapshotTests
{
    private static readonly DateTimeOffset BuiltAt = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly IndexedEntity MoltenEdge = new(ResourceType.Items, "3371");
    private static readonly IndexedEntity Boots = new(ResourceType.Items, "1001");
    private static readonly IndexedEntity Garen = new(ResourceType.Champions, "Garen");
    private static readonly StateLocation Outside = StateLocation.Outside;

    private static readonly EntityStateSnapshot Snapshot = EntityStateSnapshot.Of(
        new StateIndexGeneration
        {
            Id = 7,
            Scheme = 1,
            BuiltAt = BuiltAt,
            Versions = [V("90.1.1"), V("90.2.1"), V("90.3.1"), V("90.4.1"), V("90.5.1")],
        },
        [
            State(MoltenEdge, "90.1.1", "90.2.1") with { ClosedAt = BuiltAt },
            State(MoltenEdge, "90.3.1", "90.4.1") with
            {
                ClosedAt = BuiltAt.AddDays(3),
                PreviousOpener = V("90.1.1"),
                ChangedFields = [FieldKey.Of(ChangeField.Stat, "attack_damage")],
            },
            State(Boots, "90.1.1", null),
            State(Garen, "90.1.1", "90.2.1") with { ClosedAt = BuiltAt },
            State(Garen, "90.4.1", null) with { PreviousOpener = V("90.1.1"), IsReturn = true },
        ],
        [new PromotedVersion(V("90.4.1"), BuiltAt.AddDays(10))]);

    [Fact]
    public void CoveredVersionFindsTheStateThatHoldsIt()
    {
        var closed = Snapshot.Locate(MoltenEdge, V("90.2.1"), V("90.5.1"));
        var absent = Snapshot.Locate(Garen, V("90.3.1"), V("90.5.1"));

        Assert.Equal(StateLocationKind.Covered, closed.Kind);
        Assert.Equal(V("90.1.1"), closed.State?.Opener);
        Assert.Equal(StateLocationKind.Covered, absent.Kind);
        Assert.Null(absent.State);
    }

    [Fact]
    public void VersionPastTheCoverageReadsAsTheOpenStateUpToTheLatest()
    {
        var lagging = Snapshot.Locate(Boots, V("90.6.1"), V("90.6.1"));

        Assert.Equal(StateLocationKind.Lagging, lagging.Kind);
        Assert.True(lagging.State?.IsOpen);
        Assert.Equal(Outside, Snapshot.Locate(MoltenEdge, V("90.6.1"), V("90.6.1")));
        Assert.Equal(Outside, Snapshot.Locate(Boots, V("90.6.1"), V("90.5.1")));
        Assert.Equal(Outside, Snapshot.Locate(Boots, V("80.1.1"), V("90.5.1")));
        Assert.Equal(Outside, Snapshot.Locate(Boots, V("90.2.1"), null));
    }

    [Fact]
    public void IdIsFoldedWhateverItsCase()
    {
        var garen = new IndexedEntity(ResourceType.Champions, "GAREN");

        Assert.Equal(2, Snapshot.StatesOf(garen).Count);
        Assert.Equal(EntityChangeKind.Returned, Snapshot.StatesOf(garen)[1].Kind);
    }

    [Fact]
    public void VersionChangesWhatOpensOrRetiresThere()
    {
        Assert.False(Snapshot.HasPatchPage(V("90.1.1")));
        Assert.False(Snapshot.HasChanges(V("90.2.1")));
        Assert.Equal(["3371"], Snapshot.OpenedAt(V("90.3.1")).Select(static s => s.Key));
        Assert.Equal(["3371"], Snapshot.RetiredAt(V("90.5.1")).Select(static s => s.Key));
        Assert.Equal(["garen"], Snapshot.RetiredAt(V("90.3.1")).Select(static s => s.Key));
        Assert.True(Snapshot.HasChanges(V("90.5.1")));
    }

    [Fact]
    public void ClosedStatesAreFoundByTheirLastVersionAndTheirNext()
    {
        var closed = Snapshot.ClosedBetween(V("90.4.1"), V("90.5.1"));
        var first = Snapshot.StatesOf(MoltenEdge)[0];

        Assert.Equal([V("90.3.1")], closed.Select(static state => state.Opener));
        Assert.Equal(V("90.3.1"), Snapshot.NextOf(first)?.Opener);
        Assert.Null(Snapshot.RetirementOf(first));
        Assert.Equal(V("90.5.1"), Snapshot.RetirementOf(Snapshot.StatesOf(MoltenEdge)[1]));
    }

    [Fact]
    public void PagesAreDatedByThePromotionThatMadeTheirVersionOlder()
    {
        Assert.Equal(BuiltAt.AddDays(10), Snapshot.PatchPageModifiedAt(V("90.3.1")));
        Assert.Equal(BuiltAt, Snapshot.PatchPageModifiedAt(V("90.4.1")));
        Assert.Equal(BuiltAt.AddDays(10), Snapshot.VersionFileModifiedAt(V("90.3.1")));
        Assert.Null(Snapshot.VersionFileModifiedAt(V("90.2.1")));
        Assert.Equal(new StateIndexStamp(7, V("90.5.1")), Snapshot.Stamp);
    }

    [Fact]
    public void PromotionReplacedTheNewestVersionPromotedBeforeItWhateverWasListedBetween()
    {
        var skipped = EntityStateSnapshot.Of(
            Snapshot.Generation!,
            Snapshot.States,
            [
                new PromotedVersion(V("90.4.1"), BuiltAt.AddDays(10)),
                new PromotedVersion(V("90.2.1"), BuiltAt.AddDays(2)),
            ]);

        Assert.Equal(V("90.2.1"), skipped.PromotedBefore(V("90.4.1")));
        Assert.Equal(V("90.4.1"), skipped.PromotedBefore(V("90.5.1")));
        Assert.Null(skipped.PromotedBefore(V("90.2.1")));
        Assert.Null(EntityStateSnapshot.Empty.PromotedBefore(V("90.4.1")));
    }

    private static PatchVersion V(string version) => PatchVersion.Parse(version);

    private static IndexedState State(IndexedEntity entity, string opener, string? last) =>
        new()
        {
            Resource = entity.Resource,
            Key = entity.Key,
            Opener = V(opener),
            Last = last is null ? null : V(last),
            Fingerprint = new string('a', 64),
            Path = $"{entity.Resource}/{entity.Id}",
            ChangedFields = [],
        };
}
