using LoDb.Api.Modules.Catalog.Http;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.States;
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
/// while the lists read the same, so most calls neither serialize nor hash. The oldest and the
/// newest pages of changes come from the index of states; an index that cannot be read leaves
/// them null rather than failing every page the renderer draws.
/// </remarks>
internal sealed partial class MetaEndpoint(
    ICatalogReader reader,
    IEntityStateIndex states,
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
            var changes = await ChangesAsync(cancellationToken);
            var answer = AnswerOf(new MetaSource(versions, languages, changes));
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

        var meta = Build(source);
        var fresh = new MetaAnswer(source, meta, JsonBody.Of(meta, json.Value.SerializerOptions));
        Volatile.Write(ref last, fresh);
        return fresh;
    }

    private static CatalogMeta Build(MetaSource source) => new()
    {
        Latest = source.Versions.Latest?.Value,
        Versions = [.. source.Versions.Listed.Select(static version => version.Value)],
        ReadyVersions = [.. source.Versions.Ready.Select(static version => version.Value)],
        OldestChangesVersion = source.Changes?.Oldest.Value,
        NewestChangesVersion = source.Changes?.Newest.Value,
        VersionPattern = PatchVersion.Pattern,
        Languages = [.. source.Languages.Select(static language => language.Code)],
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

    // Every covered version from the one after the coverage start has a page of changes, up
    // to the coverage end, which lags behind the latest version while the index does.
    private async Task<ChangesWindow?> ChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var coverage = (await states.GetAsync(cancellationToken)).Coverage;
            return coverage.Start is { } start && coverage.After(start) is { } oldest
                ? new ChangesWindow(oldest, coverage.End!)
                : null;
        }
        catch (StateIndexUnavailableException exception)
        {
            LogStatesFailed(logger, exception);
            return null;
        }
    }

    [LoggerMessage(
        EventName = "catalog.meta.failed",
        Level = LogLevel.Warning,
        Message = "Meta unavailable: Data Dragon's lists could not be read.")]
    private static partial void LogListsFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "catalog.meta.degraded",
        Level = LogLevel.Warning,
        Message = "Meta without its pages of changes: the index of states is unreadable.")]
    private static partial void LogStatesFailed(ILogger logger, Exception exception);

    // The versions that have a page of changes, oldest and newest.
    private sealed record ChangesWindow(PatchVersion Oldest, PatchVersion Newest);

    // The lists an answer is built from; the rest of the answer is constant.
    private sealed record MetaSource(
        CatalogVersions Versions,
        IReadOnlyList<DdragonLanguage> Languages,
        ChangesWindow? Changes)
    {
        public bool Matches(MetaSource other) =>
            Versions.Latest == other.Versions.Latest
            && Versions.Listed.SequenceEqual(other.Versions.Listed)
            && Versions.Ready.SequenceEqual(other.Versions.Ready)
            && Languages.SequenceEqual(other.Languages)
            && Changes == other.Changes;
    }

    private sealed record MetaAnswer(MetaSource Source, CatalogMeta Meta, JsonBody Body);
}
