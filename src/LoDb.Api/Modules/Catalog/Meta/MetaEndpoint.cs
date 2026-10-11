using LoDb.Api.Modules.Catalog.Http;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace LoDb.Api.Modules.Catalog.Meta;

/// <summary>
/// <c>GET /api/meta</c>: the lists the front would otherwise copy, read from Data Dragon's
/// own lists and the domain.
/// </summary>
/// <remarks>
/// Short-lived: a promotion changes <c>latest</c>, and the reader already caches the lists
/// it is built from. The renderer asks for it on every page: the body and its tag are kept
/// while the lists read the same, so most calls neither serialize nor hash.
/// </remarks>
internal sealed partial class MetaEndpoint(
    ICatalogReader reader,
    IOptions<JsonOptions> json,
    ILogger<MetaEndpoint> logger)
{
    private MetaAnswer? last;

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
            var answer = AnswerOf(new MetaSource(versions, languages));
            return new CachedJson<CatalogMeta>(answer.Meta, CacheHeaders.Current, answer.Body);
        }
        catch (EgressException exception)
        {
            LogListsFailed(logger, exception);
            return CatalogProblem.UpstreamUnavailable();
        }
    }

    // A race only builds the same answer twice; the last one written is kept.
    private MetaAnswer AnswerOf(MetaSource source)
    {
        if (Volatile.Read(ref last) is { } held && held.Source.Matches(source))
        {
            return held;
        }

        var meta = Build(source.Versions, source.Languages);
        var fresh = new MetaAnswer(source, meta, JsonBody.Of(meta, json.Value.SerializerOptions));
        Volatile.Write(ref last, fresh);
        return fresh;
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

    // The lists an answer is built from; the rest of the answer is constant.
    private sealed record MetaSource(
        CatalogVersions Versions,
        IReadOnlyList<DdragonLanguage> Languages)
    {
        public bool Matches(MetaSource other) =>
            Versions.Latest == other.Versions.Latest
            && Versions.Listed.SequenceEqual(other.Versions.Listed)
            && Versions.Ready.SequenceEqual(other.Versions.Ready)
            && Languages.SequenceEqual(other.Languages);
    }

    private sealed record MetaAnswer(MetaSource Source, CatalogMeta Meta, JsonBody Body);
}
