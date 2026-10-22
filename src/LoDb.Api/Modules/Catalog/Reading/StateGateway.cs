using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.States.Details;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Canonicals;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// Reads where a detail's page stands in the index of states, and what its state changed, or
/// tells why it cannot yet: the index unreadable, a catalog the answer needs not stored (503
/// with <c>Retry-After</c>), or Data Dragon failed (503).
/// </summary>
/// <remarks>
/// <para>
/// The latest version is the catalog reader's; a pinned page also reads the latest en_US
/// catalog, pinned in memory, which tells the entity's short URL. Neither the canonical nor the
/// history reads any other catalog.
/// </para>
/// <para>
/// An opener's changes read one more catalog, of the last version of the previous state in
/// the page's language, admitted as the page's own: within the budget of cold work, as the
/// caller is. The short URL goes without the block while that catalog is not stored, which is
/// acceptable for the latest version; a pinned opener never does: it answers 503 and is never
/// stored without its block. Where Data Dragon did not ship the page's language yet at that
/// version, that catalog holds en_US: the block then compares only what reads the same in
/// every language (<see cref="ComparedProjection"/>).
/// </para>
/// </remarks>
internal sealed partial class StateGateway(
    IEntityStateIndex index,
    ICatalogReader reader,
    ILogger<StateGateway> logger)
{
    /// <param name="entity">The detail's entity, and the catalog of its page.</param>
    /// <param name="caller">Who asks, as the page's own read.</param>
    /// <param name="cancellationToken">The request's.</param>
    public async Task<StateRead> ReadAsync(
        DetailEntity entity,
        ColdCaller caller,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        try
        {
            return await LocateAsync(entity, ColdDemand.Synchronous.By(caller), cancellationToken);
        }
        catch (StateIndexUnavailableException exception)
        {
            LogIndexUnavailable(logger, entity.Catalog.Version.Value, exception);
            return StateRead.Fail(CatalogProblem.StateIndexUnavailable());
        }
        catch (EgressException exception)
        {
            LogUpstreamFailed(logger, entity.Catalog.Version.Value, exception);
            return StateRead.Fail(CatalogProblem.UpstreamUnavailable());
        }
    }

    private async Task<StateRead> LocateAsync(
        DetailEntity entity,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var view = await ViewAsync(entity.Catalog, demand, cancellationToken);
        if (view is null)
        {
            return StateRead.Fail(CatalogProblem.Pending());
        }

        var standing = view.Locate(entity.Path, entity.Catalog.Version);
        var state = DetailStates.Of(view, standing, entity);
        if (!standing.IsOpener)
        {
            return StateRead.Read(state);
        }

        var opener = new Opener(view, standing, entity);
        return await WithChangesAsync(state, opener, demand, cancellationToken);
    }

    // The latest catalog a pinned page reads is en_US: paths and presence do not depend on the
    // language. A page at the latest version reads its own.
    private async Task<StateView?> ViewAsync(
        CatalogSnapshot page,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var snapshot = await index.GetAsync(cancellationToken);
        var latest = await reader.GetLatestAsync(cancellationToken);
        if (latest is null || latest == page.Version)
        {
            return new StateView(snapshot, latest, latest is null ? null : page);
        }

        var load = await reader.GetAsync(latest, DdragonLanguage.EnUs, demand, cancellationToken);
        return load.IsReady ? new StateView(snapshot, latest, load.Catalog) : null;
    }

    private async Task<StateRead> WithChangesAsync(
        DetailState state,
        Opener opener,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var held = opener.Standing.State!;
        if (opener.View.PreviousOf(held) is not { Last: { } compared })
        {
            return StateRead.Read(state with { Changes = StateChanges.Uncompared(held) });
        }

        var catalog = await ComparedAsync(opener, compared, demand, cancellationToken);
        if (catalog is not null)
        {
            var changes = StateChanges.Of(held, opener.Entity, catalog);
            return StateRead.Read(state with { Changes = changes });
        }

        return opener.Standing.IsLatest
            ? StateRead.Read(state)
            : StateRead.Fail(CatalogProblem.Pending());
    }

    // Null while the catalog is not stored; on the short URL, also when Data Dragon fails,
    // which only costs the latest page its block.
    private async Task<CatalogSnapshot?> ComparedAsync(
        Opener opener,
        PatchVersion compared,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var language = opener.Entity.Catalog.Language;
        try
        {
            var load = await reader.GetAsync(compared, language, demand, cancellationToken);
            return load.IsReady ? load.Catalog : null;
        }
        catch (EgressException exception) when (opener.Standing.IsLatest)
        {
            LogUpstreamFailed(logger, compared.Value, exception);
            return null;
        }
    }

    [LoggerMessage(
        EventName = "catalog.state.unavailable",
        Level = LogLevel.Warning,
        Message = "State of a {Version} page unknown: the index of states could not be read.")]
    private static partial void LogIndexUnavailable(
        ILogger logger,
        string version,
        Exception exception);

    [LoggerMessage(
        EventName = "catalog.state.failed",
        Level = LogLevel.Warning,
        Message = "State of a {Version} page unknown: Data Dragon failed.")]
    private static partial void LogUpstreamFailed(
        ILogger logger,
        string version,
        Exception exception);

    /// <summary>The page of an opener, where it stands and what it shows.</summary>
    private sealed record Opener(StateView View, PageStanding Standing, DetailEntity Entity);
}
