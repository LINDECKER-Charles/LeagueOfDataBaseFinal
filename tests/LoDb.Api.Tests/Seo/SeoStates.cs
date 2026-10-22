using LoDb.Domain.Catalog;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The index of states the SEO tests read, over five recorded versions, one past version each
/// case: the coverage start with a closed opener, a version that changed nothing, a new entity
/// gone the next patch, a patch that closed a state and changed another, and the latest one,
/// whose promotion closed that state.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>7.21.1, the start: Ahri opens, still open; Boots opens, closed at 8.7.1. No patch
/// page; its file lists the Boots opener.</item>
/// <item>7.22.1: nothing opens nor leaves; its file is empty and not announced.</item>
/// <item>8.7.1: Molten Edge is new, gone at 16.18.1: its patch page and opener.</item>
/// <item>16.18.1: Boots changes (open since), Molten Edge leaves, Trinity Force opens and closes
/// there: its patch page and the Trinity Force opener, closed by the promotion of
/// 16.19.1.</item>
/// <item>16.19.1, the latest: Trinity Force changes again.</item>
/// </list>
/// </remarks>
public static class SeoStates
{
    public const string Start = "7.21.1";
    public const string Unchanged = "7.22.1";
    public const string Archived = "8.7.1";
    public const string Previous = "16.18.1";
    public const string Latest = "16.19.1";

    /// <summary>The promotion of <see cref="Latest"/>, after the generation was built.</summary>
    public static readonly DateTimeOffset LatestPromotedAt = TestStates.BuiltAt.AddDays(1);

    /// <summary>When the promotion of <see cref="Latest"/> closed a Trinity Force state.</summary>
    public static readonly DateTimeOffset TrinityClosedAt = LatestPromotedAt.AddMinutes(5);

    private static readonly IndexedEntity Ahri = new(ResourceType.Champions, "Ahri");
    private static readonly IndexedEntity Boots = new(ResourceType.Items, "1001");
    private static readonly IndexedEntity MoltenEdge = new(ResourceType.Items, "3371");
    private static readonly IndexedEntity Trinity = new(ResourceType.Items, "3078");

    private static readonly PromotedVersion[] Promotions =
    [
        new(PatchVersion.Parse(Previous), TestStates.BuiltAt.AddDays(-1)),
        new(PatchVersion.Parse(Latest), LatestPromotedAt),
    ];

    public static EntityStateSnapshot Snapshot() =>
        TestStates.Snapshot(
            [Start, Unchanged, Archived, Previous, Latest],
            [
                TestStates.Open(Ahri, Start),
                TestStates.Closed(Boots, Start, Archived) with { Path = "items/1001-boots" },
                Changed(TestStates.Open(Boots, Previous), Start),
                TestStates.Closed(MoltenEdge, Archived, Archived)
                    with { Path = "items/3371-molten-edge" },
                TestStates.Closed(Trinity, Previous, Previous) with
                {
                    Path = "items/3078-trinity-force",
                    ClosedAt = TrinityClosedAt,
                },
                Changed(TestStates.Open(Trinity, Latest), Previous),
            ],
            Promotions);

    /// <summary>
    /// The same history while the index lags a version behind the catalog: it ends at
    /// <see cref="Previous"/>, which still holds Boots and Trinity Force open, although
    /// <see cref="Latest"/> is promoted.
    /// </summary>
    public static EntityStateSnapshot Lagging() =>
        TestStates.Snapshot(
            [Start, Unchanged, Archived, Previous],
            [
                TestStates.Open(Ahri, Start),
                TestStates.Closed(Boots, Start, Archived) with { Path = "items/1001-boots" },
                Changed(TestStates.Open(Boots, Previous), Start),
                TestStates.Closed(MoltenEdge, Archived, Archived)
                    with { Path = "items/3371-molten-edge" },
                TestStates.Open(Trinity, Previous) with { Path = "items/3078-trinity-force" },
            ],
            Promotions);

    private static IndexedState Changed(IndexedState state, string previousOpener) =>
        state with
        {
            PreviousOpener = PatchVersion.Parse(previousOpener),
            ChangedFields = [FieldKey.Of(ChangeField.Description)],
        };
}
