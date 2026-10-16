using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Legacy.Paths;
using LoDb.Api.Modules.Legacy.Targets;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
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
/// The latest catalog answers first, held in memory and with ids stable across versions. An
/// entry it holds lands on its short URL, whatever version the old URL named: the pinned page
/// of an entry still in the game is canonical to it (ADR 0005, amendment of 2026-10-14), and
/// pinned renders are the SSR's scarcest. So does a list, a pinned one being canonical to the
/// latest bare list.
/// </para>
/// <para>
/// Only a name the latest catalog lacks, once read, opens the catalog of the version the URL
/// names, and only if that version is stored: the entry left the game, and keeps the pinned
/// page and slug of that version. A version that is not stored stays unread: its list stands
/// in, by a 302, for an entry that may have left the game, which must not become a 404.
/// </para>
/// </remarks>
internal sealed partial class LegacyResolver(
    ICatalogReader reader,
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
        var latest = await OpenAsync(lookup.Latest, lookup.Demand, cancellationToken);
        if (Find(latest, lookup) is { } fromLatest)
        {
            return lookup.MovedToLatest(fromLatest);
        }

        // A latest catalog not read yet may still hold the name, whose target is then its short
        // URL: nothing answers before. Before any promotion, only the version's own can tell.
        var isLatestUnread = latest is { IsReady: false };
        if (isLatestUnread || lookup.Version.Pinned is not { } pinned)
        {
            return latest is { IsReady: true } ? UnknownEntity(lookup) : CatalogProblem.Pending();
        }

        var own = await OpenAsync(pinned, lookup.Demand, cancellationToken);
        return own switch
        {
            { IsReady: true } => Find(own, lookup) is { } path
                ? lookup.MovedToPinned(path)
                : UnknownEntity(lookup),
            // The list stands in only for a name the latest catalog, once read, lacks too:
            // before any promotion, nothing tells yet whether the entry left the game.
            { Status: CatalogLoadStatus.Pending, Refused: false } when latest is { IsReady: true }
                => lookup.ToList(),
            _ => CatalogProblem.Pending(),
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

    /// <summary>An old detail URL being resolved, and where its redirects land.</summary>
    private sealed record EntityLookup(
        LegacyPath Legacy,
        LegacyVersion Version,
        LegacyLocale Locale,
        string Segment)
    {
        public PatchVersion? Latest { get; init; }

        public required ColdDemand Demand { get; init; }

        /// <summary>An entry of the latest patch: its short URL.</summary>
        public LegacyRedirect MovedToLatest(CanonicalPath path) =>
            LegacyRedirect.Permanent(LegacyTarget.Of(Locale, null, path.Value));

        /// <summary>An entry gone from the latest patch: its page in the version named.</summary>
        public LegacyRedirect MovedToPinned(CanonicalPath path) =>
            LegacyRedirect.Permanent(LegacyTarget.Of(Locale, Version.Pinned, path.Value));

        public LegacyRedirect ToList() =>
            LegacyRedirect.ToList(LegacyTarget.Of(Locale, Version.Pinned, Segment));
    }
}
