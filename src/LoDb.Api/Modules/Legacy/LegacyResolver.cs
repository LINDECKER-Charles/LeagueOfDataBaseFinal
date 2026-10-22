using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Legacy.Paths;
using LoDb.Api.Modules.Legacy.Targets;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Legacy;

/// <summary>
/// Resolves an old URL into the one redirect that lands on its new form, or tells why none
/// does.
/// </summary>
/// <remarks>
/// <para>
/// The old names are looked up in en_US catalogs: keys do not depend on the language, and
/// en_US is the one every version ships. An old URL never ingests anything: crawlers follow
/// them by the thousand, across every version.
/// </para>
/// <para>
/// The latest catalog answers first, held in memory and with ids stable across versions: it
/// turns the old name into an id. The index of states then lands the URL on the canonical of
/// the page it named (ADR 0005, amendments of 2026-10-16 and 2026-10-22), without opening any
/// other catalog: the opener of the state that held the version, the short URL while that
/// state is open. Without a version, an entry the latest catalog holds lands on its short URL,
/// one it lacks on the opener of its last state. A list lands on the latest bare list, a pinned
/// one being canonical to it.
/// </para>
/// <para>
/// Where the index has no state at the version named, an entry the latest catalog holds lands
/// on its short URL. Only a name it lacks, once read, opens the catalog of the version the URL
/// names, and only if that version is stored: the entry keeps the pinned page and slug of
/// that version. A version that is not stored stays unread: its list stands in, by a 302, for
/// an entry that may have left the game, which must not become a 404. A version listed after
/// the latest one never asks the index.
/// </para>
/// </remarks>
internal sealed partial class LegacyResolver(
    ICatalogReader reader,
    IEntityStateIndex states,
    ILogger<LegacyResolver> logger)
{
    public async Task<Results<LegacyRedirect, CatalogProblem>> ResolveAsync(
        LegacyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var legacy = LegacyPathParser.Parse(request.Path);
        if (legacy is null)
        {
            return LegacyProblems.UnknownPath();
        }

        try
        {
            return legacy.Kind == LegacyPathKind.Page
                ? LegacyRedirect.Permanent(LegacyTarget.Of(
                    await LocaleAsync(request.Language, cancellationToken),
                    null,
                    legacy.PagePath))
                : await ResolveCatalogAsync(legacy, request, cancellationToken);
        }
        catch (EgressException exception)
        {
            LogListsFailed(logger, exception);
            return CatalogProblem.UpstreamUnavailable();
        }
        catch (StateIndexUnavailableException exception)
        {
            LogStatesFailed(logger, exception);
            return CatalogProblem.StateIndexUnavailable();
        }
    }

    private static CatalogProblem UnknownEntity(EntityLookup lookup) =>
        CatalogProblem.UnknownEntity(lookup.Segment, lookup.Legacy.Name);

    private static CanonicalPath? Find(CatalogLoad? load, EntityLookup lookup) =>
        load is { IsReady: true }
            ? LegacyEntityLookup.Find(load.Catalog, lookup.Legacy.Resource, lookup.Legacy.Name)
            : null;

    private async Task<Results<LegacyRedirect, CatalogProblem>> ResolveCatalogAsync(
        LegacyPath legacy,
        LegacyRequest request,
        CancellationToken cancellationToken)
    {
        var versions = await reader.GetVersionsAsync(cancellationToken);
        var version = LegacyVersions.Choose(legacy.Version, request.Version, versions);
        if (version is null)
        {
            return CatalogProblem.UnknownVersion(legacy.Version ?? string.Empty);
        }

        var locale = await LocaleAsync(request.Language, cancellationToken);
        var segment = CanonicalPath.SegmentOf(legacy.Resource);
        if (legacy.Kind == LegacyPathKind.List)
        {
            // Before any promotion, no short list exists yet to land on.
            var listVersion = versions.Latest is null ? version.Pinned : null;
            return LegacyRedirect.Permanent(LegacyTarget.Of(locale, listVersion, segment));
        }

        var lookup = new EntityLookup(legacy, version, locale, segment)
        {
            Latest = versions.Latest,
            Demand = ColdDemand.StoredOnly.By(request.Caller),
        };
        return await ResolveEntityAsync(lookup, cancellationToken);
    }

    private async Task<Results<LegacyRedirect, CatalogProblem>> ResolveEntityAsync(
        EntityLookup lookup,
        CancellationToken cancellationToken)
    {
        // A latest catalog not read yet may still hold the name, whose target is then its short
        // URL: nothing answers before.
        var latest = await OpenAsync(lookup.Latest, lookup.Demand, cancellationToken);
        if (latest is { IsReady: false })
        {
            return CatalogProblem.Pending();
        }

        var fromLatest = Find(latest, lookup);
        if (await IndexedAsync(lookup, fromLatest, cancellationToken) is { } indexed)
        {
            return indexed;
        }

        if (fromLatest is { } path)
        {
            return lookup.MovedToLatest(path);
        }

        // Without a version, the latest catalog decided once read; before any promotion,
        // nothing tells yet whether the entry exists.
        if (lookup.Version.Pinned is null)
        {
            return latest is null ? CatalogProblem.Pending() : UnknownEntity(lookup);
        }

        return await FromOwnCatalogAsync(lookup, latest, cancellationToken);
    }

    // The rule of 2026-10-16 for a name the latest catalog lacks: the catalog of the version
    // named, if stored, keeps the entry's pinned page and slug.
    private async Task<Results<LegacyRedirect, CatalogProblem>> FromOwnCatalogAsync(
        EntityLookup lookup,
        CatalogLoad? latest,
        CancellationToken cancellationToken)
    {
        var own = await OpenAsync(lookup.Version.Pinned, lookup.Demand, cancellationToken);
        return own switch
        {
            { IsReady: true } => Find(own, lookup) is { } found
                ? lookup.MovedToPinned(found)
                : UnknownEntity(lookup),
            // The list stands in only for a name the latest catalog, once read, lacks too:
            // before any promotion, nothing tells yet whether the entry left the game.
            { Status: CatalogLoadStatus.Pending, Refused: false } when latest is not null
                => lookup.ToList(),
            _ => CatalogProblem.Pending(),
        };
    }

    // The redirect the index of states decides, if any: at the version named, the opener of a
    // closed state, the short URL of an open one; without a version, the opener of the last
    // state of an entry the latest catalog lacks. The rule of 2026-10-16 decides the rest.
    private async Task<LegacyRedirect?> IndexedAsync(
        EntityLookup lookup,
        CanonicalPath? fromLatest,
        CancellationToken cancellationToken)
    {
        var id = fromLatest?.Id ?? lookup.IndexedId;
        var pinned = lookup.Version.Pinned;
        if (id is null || lookup.Latest is not { } latest || pinned > latest
            || (pinned is null && fromLatest is not null))
        {
            return null;
        }

        var snapshot = await states.GetAsync(cancellationToken);
        var entity = new IndexedEntity(lookup.Legacy.Resource, id);
        if (pinned is null)
        {
            return snapshot.StatesOf(entity) is [.., { IsOpen: false } last]
                ? lookup.MovedToOpener(last)
                : null;
        }

        return snapshot.Locate(entity, pinned, latest).State switch
        {
            { IsOpen: false } closed => lookup.MovedToOpener(closed),
            { IsOpen: true } when fromLatest is not null => lookup.MovedToLatest(fromLatest),
            _ => null,
        };
    }

    // Stored-only: what memory or the store holds, never an ingestion.
    private async Task<CatalogLoad?> OpenAsync(
        PatchVersion? version,
        ColdDemand demand,
        CancellationToken cancellationToken) =>
        version is null
            ? null
            : await reader.GetAsync(version, DdragonLanguage.EnUs, demand, cancellationToken);

    // The list of languages is only read when a variant may have to be kept.
    private async Task<LegacyLocale> LocaleAsync(
        string? language,
        CancellationToken cancellationToken) =>
        DdragonLanguage.TryParse(language, out _)
            ? LegacyLocales.Choose(language, await reader.GetLanguagesAsync(cancellationToken))
            : LegacyLocale.Fallback;

    [LoggerMessage(
        EventName = "legacy.redirect.failed",
        Level = LogLevel.Warning,
        Message = "Former URL unresolved: Data Dragon's lists could not be read.")]
    private static partial void LogListsFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "legacy.redirect.unavailable",
        Level = LogLevel.Warning,
        Message = "Former URL unresolved: the index of states could not be read.")]
    private static partial void LogStatesFailed(ILogger logger, Exception exception);

    /// <summary>An old detail URL being resolved, and where its redirects land.</summary>
    private sealed record EntityLookup(
        LegacyPath Legacy,
        LegacyVersion Version,
        LegacyLocale Locale,
        string Segment)
    {
        public PatchVersion? Latest { get; init; }

        public required ColdDemand Demand { get; init; }

        /// <summary>
        /// The old name as the index keys an entry, which is its id but for a rune path: the
        /// old site named those by their key ("Domination"), which only a catalog turns into an
        /// id.
        /// </summary>
        public string? IndexedId =>
            Legacy.Resource == ResourceType.Runes ? null : Legacy.Name;

        /// <summary>An entry of the latest patch: its short URL.</summary>
        public LegacyRedirect MovedToLatest(CanonicalPath path) =>
            LegacyRedirect.Permanent(LegacyTarget.Of(Locale, null, path.Value));

        /// <summary>
        /// The page the index indexes for a closed state: its opener, with the slug of that
        /// version.
        /// </summary>
        public LegacyRedirect MovedToOpener(IndexedState state) =>
            LegacyRedirect.Permanent(LegacyTarget.Of(Locale, state.Opener, state.Path));

        /// <summary>An entry gone from the latest patch: its page in the version named.</summary>
        public LegacyRedirect MovedToPinned(CanonicalPath path) =>
            LegacyRedirect.Permanent(LegacyTarget.Of(Locale, Version.Pinned, path.Value));

        public LegacyRedirect ToList() =>
            LegacyRedirect.ToList(LegacyTarget.Of(Locale, Version.Pinned, Segment));
    }
}
