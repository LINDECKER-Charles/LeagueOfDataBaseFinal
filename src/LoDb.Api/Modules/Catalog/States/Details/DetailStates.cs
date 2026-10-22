using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Ingestion.Catalog.States.Canonicals;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// The state fields of a detail, from where its page stands in the index: read from the index
/// alone, but for the names of the subjects of its own changed fields.
/// </summary>
internal static class DetailStates
{
    /// <summary>The fields of the page of <paramref name="entity"/>, without its changes.</summary>
    public static DetailState Of(StateView view, PageStanding standing, DetailEntity entity)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(entity);
        return new DetailState
        {
            Canonical = CanonicalLink.Of(standing.Canonical),
            State = standing.State is null ? null : StateOf(view, standing, entity),
            History = [.. view.HistoryOf(standing).Select(state => EntryOf(view, state, entity))],
            Next = standing.State is { } held ? NextOf(view.SuccessorOf(held)) : null,
            PresentInLatest = standing.IsPresentInLatest,
            Changes = null,
        };
    }

    private static EntityState StateOf(StateView view, PageStanding standing, DetailEntity entity)
    {
        var state = standing.State!;
        var names = new EntityNames(standing.Entity, entity.Catalog, null);
        return new EntityState
        {
            OpenerVersion = state.Opener.Value,
            LastVersion = state.Last?.Value,
            IsOpen = state.IsOpen,
            IsOpener = standing.IsOpener,
            Kind = state.Kind,
            ChangedFields = ChangedFieldList.Of(state.ChangedFields, names),
            OpenerLink = CanonicalLink.Of(view.OpenerLinkOf(state, entity.Path)),
            Canonical = CanonicalLink.Of(view.LinkOf(state, entity.Path)),
        };
    }

    // An open state's range never names the latest version: its last one stays null.
    private static StateHistoryEntry EntryOf(
        StateView view,
        IndexedState state,
        DetailEntity entity) =>
        new()
        {
            OpenerVersion = state.Opener.Value,
            LastVersion = state.Last?.Value,
            Kind = state.Kind,
            ChangedFields = ChangedFieldList.Of(state.ChangedFields, null),
            Link = CanonicalLink.Of(view.LinkOf(state, entity.Path)),
        };

    // A change links the next opener's versioned URL, even while it is the latest version (a
    // 301 then): a frozen page never changes.
    private static StateNext? NextOf(StateSuccessor? successor) => successor switch
    {
        null => null,
        { Next: { } next } => new StateNext
        {
            Kind = StateNextKind.Changed,
            Version = successor.Version.Value,
            Link = CanonicalLink.Of(CanonicalTarget.OpenerOf(next)),
        },
        _ => new StateNext
        {
            Kind = StateNextKind.Removed,
            Version = successor.Version.Value,
            Link = null,
        },
    };
}
