using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.States;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Canonicals;
using LoDb.Ingestion.Catalog.States.Snapshots;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>
/// Reads the page of changes of a patch: what the index says the version changed against the
/// covered version before it, named by the catalogs that show it.
/// </summary>
/// <remarks>
/// <para>
/// Every covered version but the oldest one has a page, up to the catalog reader's latest
/// version: the oldest, a version outside the index and one listed after the latest answer
/// 404 before any catalog is read.
/// </para>
/// <para>
/// The entities whose state opens at the version are named by its catalog in the page's
/// language; those it retired, by the catalog of the version before: two catalogs when the
/// version retires something. Their icons, like a list's, may stay placeholders a moment.
/// </para>
/// </remarks>
internal sealed partial class PatchChangesGateway(
    IEntityStateIndex index,
    ICatalogReader reader,
    CatalogGateway catalogs,
    ILogger<PatchChangesGateway> logger)
{
    public async Task<Results<CachedJson<PatchChanges>, CatalogProblem>> ReadAsync(
        CatalogScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!PatchVersion.TryParse(scope.Version, out var version))
        {
            return CatalogProblem.InvalidVersion(scope.Version);
        }

        if (!DdragonLanguage.TryParse(scope.Language, out _))
        {
            return CatalogProblem.InvalidLanguage(scope.Language);
        }

        var located = await LocateAsync(version, cancellationToken);
        if (located.Patch is not { } patch)
        {
            return located.Problem!;
        }

        var page = await catalogs.ReadAsync(scope, cancellationToken);
        return page.IsOpen
            ? await WithRetiredAsync(patch, scope, page.Context, cancellationToken)
            : page.Problem;
    }

    // The catalog of the version before, read only when the version retired something.
    private async Task<Results<CachedJson<PatchChanges>, CatalogProblem>> WithRetiredAsync(
        Patch patch,
        CatalogScope scope,
        CatalogContext page,
        CancellationToken cancellationToken)
    {
        if (patch.Snapshot.RetiredAt(patch.Version).Count == 0)
        {
            return await AnswerAsync(patch, page, null, cancellationToken);
        }

        var previous = scope with { Version = patch.Previous.Value };
        var before = await catalogs.ReadAsync(previous, cancellationToken);
        return before.IsOpen
            ? await AnswerAsync(patch, page, before.Context, cancellationToken)
            : before.Problem;
    }

    private async Task<Located> LocateAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await index.GetAsync(cancellationToken);
            var latest = await reader.GetLatestAsync(cancellationToken);
            return latest is null || version > latest || !snapshot.HasPatchPage(version)
                ? new Located(null, CatalogProblem.NoPatchPage(version.Value))
                : new Located(new Patch(snapshot, version, latest), null);
        }
        catch (StateIndexUnavailableException exception)
        {
            LogIndexUnavailable(logger, version.Value, exception);
            return new Located(null, CatalogProblem.StateIndexUnavailable());
        }
    }

    private static async Task<CachedJson<PatchChanges>> AnswerAsync(
        Patch patch,
        CatalogContext page,
        CatalogContext? before,
        CancellationToken cancellationToken)
    {
        var opened = await patch.NameAsync(
            page,
            patch.Snapshot.OpenedAt(patch.Version),
            cancellationToken);
        var retired = before is null ? Named.None : await patch.NameAsync(
            before,
            patch.Snapshot.RetiredAt(patch.Version),
            cancellationToken);
        var changes = new PatchChanges
        {
            Version = patch.Version.Value,
            Language = page.Catalog.Language.Code,
            PreviousVersion = patch.Previous.Value,
            HasChanges = patch.Snapshot.HasChanges(patch.Version),
            Groups = GroupsOf(opened.Entries.Concat(retired.Entries)),
        };
        return page.Answer(changes, retired.Images.HasPending ? retired.Images : opened.Images);
    }

    // One group per resource, in their order; in each, new, changed, returned, then retired
    // entities, each by name.
    private static PatchChangeGroup[] GroupsOf(
        IEnumerable<(ResourceType Type, PatchChangeEntry Entry)> entries) =>
        [.. entries
            .GroupBy(static pair => pair.Type)
            .OrderBy(static group => group.Key)
            .Select(static group => new PatchChangeGroup
            {
                Type = group.Key,
                Entries = [.. group.Select(static pair => pair.Entry)
                    .OrderBy(static entry => entry.Kind)
                    .ThenBy(static entry => entry.Name, StringComparer.Ordinal)],
            })];

    [LoggerMessage(
        EventName = "catalog.changes.unavailable",
        Level = LogLevel.Warning,
        Message = "Changes of {Version} unknown: the index of states could not be read.")]
    private static partial void LogIndexUnavailable(
        ILogger logger,
        string version,
        Exception exception);

    /// <summary>A version with a page of changes, and the index that says so.</summary>
    private sealed record Patch(
        EntityStateSnapshot Snapshot,
        PatchVersion Version,
        PatchVersion Latest)
    {
        public PatchVersion Previous => Snapshot.Coverage.Before(Version)!;

        /// <summary>
        /// The entities of <paramref name="states"/> as the catalog of
        /// <paramref name="context"/> names them, their icons resolved as a list's.
        /// </summary>
        public async Task<Named> NameAsync(
            CatalogContext context,
            IReadOnlyList<IndexedState> states,
            CancellationToken cancellationToken)
        {
            PatchEntity[] named = [.. states.Select(state =>
                PatchEntity.Of(state, KindOf(state), context.Catalog))];
            var images = await context.ResolveAsync(
                named.Select(static entity => entity.Image),
                ColdDemand.Queued,
                cancellationToken);
            return new Named(
                [.. named.Select(entity => (
                    entity.State.Resource,
                    entity.ToEntry(images, context.Catalog, LinkOf(entity))))],
                images);
        }

        // A state opened at the version, else the last state of an entity it retired.
        private PatchChangeKind KindOf(IndexedState state) =>
            state.Opener != Version ? PatchChangeKind.Retired
            : state.Kind switch
            {
                EntityChangeKind.New => PatchChangeKind.New,
                EntityChangeKind.Returned => PatchChangeKind.Returned,
                _ => PatchChangeKind.Changed,
            };

        // The entity's page at the version, its short URL at the latest one; a retired
        // entity's, the opener of its last state.
        private CanonicalLink LinkOf(PatchEntity entity) => CanonicalLink.Of(
            entity.Kind == PatchChangeKind.Retired ? CanonicalTarget.OpenerOf(entity.State)
            : Version == Latest ? CanonicalTarget.ShortUrl(entity.State.Path)
            : CanonicalTarget.Pinned(Version, entity.State.Path));
    }

    /// <summary>The entries one catalog named, and the icons it resolved for them.</summary>
    private sealed record Named(
        IReadOnlyList<(ResourceType Type, PatchChangeEntry Entry)> Entries,
        ImageSet Images)
    {
        public static Named None { get; } = new([], ImageSet.Empty);
    }

    private sealed record Located(Patch? Patch, CatalogProblem? Problem);
}
