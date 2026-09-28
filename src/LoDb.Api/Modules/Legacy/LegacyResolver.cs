using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Legacy.Paths;
using LoDb.Api.Modules.Legacy.Targets;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Legacy;

/// <summary>
/// Resolves an old URL into the one 301 that lands on its new form, or tells why none does.
/// </summary>
/// <remarks>
/// The old names are looked up in the en_US catalog of the version: keys do not depend on the
/// language, and en_US is the one every version ships. A cold version is ingested before the
/// answer, as the page the redirect leads to would do anyway.
/// </remarks>
internal sealed partial class LegacyResolver(
    ICatalogReader reader,
    CatalogGateway gateway,
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
                ? new LegacyRedirect(LegacyTarget.Of(
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
            return new LegacyRedirect(LegacyTarget.Of(locale, version.Pinned, segment));
        }

        var read = await OpenAsync(version.Catalog, cancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var path = LegacyEntityLookup.Find(read.Context.Catalog, legacy.Resource, legacy.Name);
        return path is null
            ? CatalogProblem.UnknownEntity(segment, legacy.Name)
            : new LegacyRedirect(LegacyTarget.Of(locale, version.Pinned, path.Value));
    }

    // The list of languages is only read when a variant may have to be kept.
    private async Task<LegacyLocale> LocaleAsync(
        string? language,
        CancellationToken cancellationToken) =>
        DdragonLanguage.TryParse(language, out _)
            ? LegacyLocales.Choose(language, await reader.GetLanguagesAsync(cancellationToken))
            : LegacyLocale.Fallback;

    private Task<CatalogRead> OpenAsync(
        PatchVersion? version,
        CancellationToken cancellationToken) =>
        version is null
            ? Task.FromResult(CatalogRead.Fail(CatalogProblem.Pending()))
            : gateway.ReadAsync(
                new CatalogScope(version.Value, DdragonLanguage.EnUs.Code),
                cancellationToken);

    [LoggerMessage(
        EventName = "legacy.redirect.failed",
        Level = LogLevel.Warning,
        Message = "Former URL unresolved: Data Dragon's lists could not be read.")]
    private static partial void LogListsFailed(ILogger logger, Exception exception);
}
