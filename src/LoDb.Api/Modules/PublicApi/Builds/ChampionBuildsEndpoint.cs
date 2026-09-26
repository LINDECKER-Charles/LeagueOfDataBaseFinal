using System.Text.Json;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.PublicApi.Gate;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Api.Modules.PublicApi.OpenApi;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Builds;

/// <summary>
/// <c>GET /v1/champions/{championId}/builds</c>: the public builds of a champion, newest
/// first, a page at a time.
/// </summary>
/// <remarks>
/// The champion id is matched exactly and never checked: an unknown one lists nothing. The
/// builds of a banned account are left out, as the site leaves them out; go-api listed
/// them.
/// </remarks>
internal sealed class ChampionBuildsEndpoint(LoDbDbContext db, IOptions<PublicApiOptions> options)
{
    public const string Pattern = "/champions/{championId}/builds";

    // The site's page of a shared build.
    private const string SharePath = "/b/";

    public static void Map(IEndpointRouteBuilder v1) =>
        v1.MapRead(
                Pattern,
                static (
                    string championId,
                    HttpContext context,
                    [FromServices] ChampionBuildsEndpoint endpoint) =>
                    endpoint.ListAsync(championId, context))
            .WithName("v1ListChampionBuilds")
            .WithSummary("The public builds of a champion, newest first, a page at a time.")
            .AddPaginationParameters()
            .ProducesV1<BuildsResponse>()
            .ProducesV1Refusals(StatusCodes.Status400BadRequest);

    public async Task<IResult> ListAsync(string championId, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Pagination.TryRead(context.Request.QueryString, out var paging))
        {
            return Pagination.Invalid;
        }

        var aborted = context.RequestAborted;
        var listed = db.Builds
            .AsNoTracking()
            .Where(build => build.ChampionId == championId
                && build.IsPublic
                && !build.Owner!.IsBanned);
        var total = await listed.LongCountAsync(aborted);
        var totalPages = paging.TotalPages(total);

        // Beyond the last page nothing is read, however large the page asked for.
        var rows = paging.Page > totalPages
            ? []
            : await listed
                .OrderByDescending(build => build.CreatedAt)
                .ThenByDescending(build => build.Id)
                .Skip((int)paging.Offset)
                .Take(paging.PerPage)
                .Select(build => new
                {
                    build.Name,
                    build.Description,
                    build.GameVersion,
                    build.Runes,
                    build.Steps,
                    build.ShareToken,
                    build.CreatedAt,
                })
                .ToListAsync(aborted);
        var siteOrigin = SiteOrigin();
        List<BuildItem> items = [.. rows.Select(row => new BuildItem(
            row.Name,
            row.Description,
            row.GameVersion,
            Document(row.Runes),
            Document(row.Steps),
            siteOrigin + SharePath + row.ShareToken,
            row.CreatedAt.UtcDateTime))];
        return V1Json.Ok(new BuildsResponse(
            championId,
            items,
            new PaginationMeta(paging.Page, paging.PerPage, total, totalPages)));
    }

    private static JsonElement Document(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    // Checked when the host starts.
    private string SiteOrigin() =>
        WebOrigin.TryParse(options.Value.SiteOrigin, out var origin)
            ? WebOrigin.Format(origin)
            : throw new InvalidOperationException("LoDb:PublicApi:SiteOrigin is not an origin.");
}
