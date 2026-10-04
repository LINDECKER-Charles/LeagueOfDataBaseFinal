using LoDb.Api.Modules.Catalog.Http;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Meta;

/// <summary>
/// <c>GET /api/meta</c>: the lists the front would otherwise copy, read from Data Dragon's
/// own lists and the domain.
/// </summary>
/// <remarks>
/// Short-lived: a promotion changes <c>latest</c>, and the reader already caches the lists
/// it is built from.
/// </remarks>
internal sealed partial class MetaEndpoint(
    ICatalogReader reader,
    ILogger<MetaEndpoint> logger)
{
    public static void Map(IEndpointRouteBuilder api) =>
        api.MapGet(
                "/meta",
                static ([FromServices] MetaEndpoint endpoint, CancellationToken aborted) =>
                    endpoint.GetAsync(aborted))
            .WithTags(CatalogTags.Meta)
            .WithName("getMeta")
            .WithSummary("Versions, languages, locales and game modes.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<CachedJson<CatalogMeta>, CatalogProblem>> GetAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var versions = await reader.GetVersionsAsync(cancellationToken);
            var languages = await reader.GetLanguagesAsync(cancellationToken);
            return new CachedJson<CatalogMeta>(Build(versions, languages), CacheHeaders.Current);
        }
        catch (EgressException exception)
        {
            LogListsFailed(logger, exception);
            return CatalogProblem.UpstreamUnavailable();
        }
    }

    private static CatalogMeta Build(
        CatalogVersions versions,
        IReadOnlyList<DdragonLanguage> languages) => new()
    {
        Latest = versions.Latest?.Value,
        Versions = [.. versions.Listed.Select(static version => version.Value)],
        ReadyVersions = [.. versions.Ready.Select(static version => version.Value)],
        VersionPattern = PatchVersion.Pattern,
        Languages = [.. languages.Select(static language => language.Code)],
        LanguagePattern = DdragonLanguage.Pattern,
        DefaultLanguage = DdragonLanguage.EnUs.Code,
        Locales = [.. UiLocales.All.Select(static locale => new LocaleLanguage
        {
            Locale = locale,
            Language = UiLocales.DefaultLanguage(locale).Code,
        })],
        FallbackLocale = UiLocales.Fallback,
        GameModes = [.. GameModes.All.Select(static mode => new GameModeMap
        {
            Mode = mode,
            Map = (int)GameModes.MapOf(mode),
        })],
        DefaultGameMode = GameModes.Default,
    };

    [LoggerMessage(
        EventName = "catalog.meta.failed",
        Level = LogLevel.Warning,
        Message = "Meta unavailable: Data Dragon's lists could not be read.")]
    private static partial void LogListsFailed(ILogger logger, Exception exception);
}
