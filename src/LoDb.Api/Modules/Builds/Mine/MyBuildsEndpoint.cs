using LoDb.Api.Modules.Builds.Catalogs;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Rendering;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Builds.Mine;

/// <summary>
/// <c>GET /api/builds</c>: the signed-in account's builds, most recently updated first.
/// </summary>
internal sealed class MyBuildsEndpoint(
    BuildAccounts accounts,
    LoDbDbContext db,
    BuildCatalogReads catalogs)
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapGet(
                string.Empty,
                static (
                    [AsParameters] BuildQuery query,
                    [FromServices] MyBuildsEndpoint endpoint,
                    HttpContext context) => endpoint.ListAsync(query, context))
            .WithName("listMyBuilds")
            .WithSummary("The signed-in account's builds, champion and keystone on the patch.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<IReadOnlyList<MyBuildRow>>, BuildProblem, CatalogProblem>>
        ListAsync(BuildQuery query, HttpContext context)
    {
        if (await accounts.FindAsync(context.User) is not { } owner)
        {
            return BuildProblem.SignedOut();
        }

        var aborted = context.RequestAborted;
        var read = await catalogs.OpenAsync(query.Browsing, aborted);
        if (BuildCatalogReads.IsClientError(read, out var problem))
        {
            return problem;
        }

        var builds = await db.Builds.AsNoTracking()
            .Where(build => build.OwnerId == owner.Id)
            .OrderByDescending(static build => build.UpdatedAt)
            .ThenByDescending(static build => build.Id)
            .ToListAsync(aborted);
        var structures = builds.Select(StoredStructures.Normalized).ToList();
        var scene = await BuildScene.OpenAsync(read.Context, structures, aborted);
        return TypedResults.Ok<IReadOnlyList<MyBuildRow>>(
            [.. builds.Select((build, index) => MyBuildRow.Of(build, structures[index], scene))]);
    }
}
