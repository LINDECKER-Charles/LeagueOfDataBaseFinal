using LoDb.Api.Modules.Catalog.Http;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using LoDb.Ingestion.Normalization;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// Opens the catalog a call names, or tells why it cannot: a malformed version or language
/// (400), one Data Dragon does not list (404), one not ingested yet or an upstream failure
/// (503 with <c>Retry-After</c>).
/// </summary>
/// <remarks>
/// The datasets of a cold version are ingested before the answer (ADR 0003): only their
/// images may stay placeholders, and only on a list. Every read is a visitor's: the crawler
/// budget applies to the pages, which know who asks.
/// </remarks>
internal sealed partial class CatalogGateway(
    ICatalogReader reader,
    IImageResolver resolver,
    ILogger<CatalogGateway> logger)
{
    public async Task<CatalogRead> ReadAsync(
        CatalogScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!PatchVersion.TryParse(scope.Version, out var version))
        {
            return CatalogRead.Fail(CatalogProblem.InvalidVersion(scope.Version));
        }

        if (!DdragonLanguage.TryParse(scope.Language, out var language))
        {
            return CatalogRead.Fail(CatalogProblem.InvalidLanguage(scope.Language));
        }

        var requested = new DatasetScope { Version = version, Language = language };
        try
        {
            var load = await reader
                .GetAsync(version, language, ColdDemand.Synchronous, cancellationToken);
            return await OpenAsync(load, requested, cancellationToken);
        }
        catch (EgressException exception)
        {
            LogUpstreamFailed(logger, version.Value, language.Code, exception);
            return CatalogRead.Fail(CatalogProblem.UpstreamUnavailable());
        }
    }

    private async Task<CatalogRead> OpenAsync(
        CatalogLoad load,
        DatasetScope requested,
        CancellationToken cancellationToken)
    {
        if (load.IsReady)
        {
            var latest = await reader.GetLatestAsync(cancellationToken);
            return CatalogRead.Open(new CatalogContext(load.Catalog, latest, resolver));
        }

        return load.Status == CatalogLoadStatus.Pending
            ? CatalogRead.Fail(CatalogProblem.Pending())
            : CatalogRead.Fail(await UnknownAsync(requested, cancellationToken));
    }

    // The load does not tell which of the two Data Dragon lacks; the version comes first, as
    // in the URL.
    private async Task<CatalogProblem> UnknownAsync(
        DatasetScope requested,
        CancellationToken cancellationToken)
    {
        var versions = await reader.GetVersionsAsync(cancellationToken);
        return versions.Listed.Contains(requested.Version)
            ? CatalogProblem.UnknownLanguage(requested.Language.Code)
            : CatalogProblem.UnknownVersion(requested.Version.Value);
    }

    [LoggerMessage(
        EventName = "catalog.read.failed",
        Level = LogLevel.Warning,
        Message = "Catalog {Version}/{Language} unavailable: Data Dragon failed.")]
    private static partial void LogUpstreamFailed(
        ILogger logger,
        string version,
        string language,
        Exception exception);
}
