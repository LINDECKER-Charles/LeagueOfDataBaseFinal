using LoDb.Api.Modules.Builds.Catalogs;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Rendering;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Votes;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Languages;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Trends;

/// <summary>
/// <c>GET /api/trends</c>: the public builds, best scored first, 24 a page, their names on
/// the version browsed.
/// </summary>
internal sealed class TrendsEndpoint(
    TrendsRanking ranking,
    BuildScores scores,
    BuildAccounts accounts,
    BuildVersions versions,
    BuildCatalogReads catalogs)
{
    public static void Map(IEndpointRouteBuilder trends) =>
        trends.MapGet(
                string.Empty,
                static (
                    [AsParameters] TrendsQuery query,
                    [FromServices] TrendsEndpoint endpoint) => endpoint.ListAsync(query))
            .WithName("listTrends")
            .WithSummary("The public builds, best scored first, filtered and paged.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<TrendsPage>, CatalogProblem>> ListAsync(TrendsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var aborted = query.Context.RequestAborted;
        var read = await catalogs.OpenAsync(query.Browsing, aborted);
        if (BuildCatalogReads.IsClientError(read, out var problem))
        {
            return problem;
        }

        var filter = await FilterAsync(query);
        var page = query.PageNumber;
        var (ranked, total) = await ranking.RankAsync(filter, page, aborted);
        var champions = await ranking.ChampionIdsAsync(aborted);
        return TypedResults.Ok(new TrendsPage
        {
            Rows = await RowsAsync(ranked, read.Context, query.Context),
            Total = total,
            Page = page,
            Pages = Math.Max(1, (total + TrendsRanking.PerPage - 1) / TrendsRanking.PerPage),
            PerPage = TrendsRanking.PerPage,
            Filters = filter,
            ChampionOptions = ChampionOption.Sorted(champions, read.Context?.Catalog),
            LanguageOptions = await ranking.LanguagesAsync(aborted),
        });
    }

    private static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<TrendsFilter> FilterAsync(TrendsQuery query) => new()
    {
        Champion = Trimmed(query.Champion),
        Mode = GameModes.TryParse(query.Mode, out var mode) ? mode : null,
        Language = await LanguageAsync(query.Language, query.Context.RequestAborted),
    };

    // A language Data Dragon's list lacks filters nothing; one it could not tell is trusted.
    private async Task<string?> LanguageAsync(
        string? requested,
        CancellationToken cancellationToken) =>
        DdragonLanguage.TryParse(Trimmed(requested), out var language)
        && await versions.IsKnownAsync(language, cancellationToken) != false
            ? language.Code
            : null;

    private async Task<IReadOnlyList<TrendRow>> RowsAsync(
        IReadOnlyList<RankedBuild> ranked,
        CatalogContext? catalog,
        HttpContext context)
    {
        var aborted = context.RequestAborted;
        List<int> ids = [.. ranked.Select(static row => row.Build.Id)];
        var votes = await scores.VotesOfAsync(accounts.IdOf(context.User), ids, aborted);
        List<BuildStructure> excerpts =
            [.. ranked.Select(row => TrendRow.Excerpt(StoredStructures.Normalized(row.Build)))];
        var scene = await BuildScene.OpenAsync(catalog, excerpts, aborted);
        return [.. ranked.Select((row, index) => TrendRow.Of(row, excerpts[index], scene) with
        {
            MyVote = votes.GetValueOrDefault(row.Build.Id),
        })];
    }
}
