using LoDb.Api.Modules.Builds.Catalogs;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Rendering;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Votes;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Builds.Metadata;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Builds;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Builds.Sharing;

/// <summary>
/// <c>GET /api/share/{token}</c>: a build for anyone holding its link, public or private,
/// rendered on the patch it is pinned to.
/// </summary>
/// <remarks>
/// A banned author's builds leave the trends, but their links keep working: the token is
/// the key, and the page never lists them anywhere.
/// </remarks>
internal sealed class ShareEndpoint(
    BuildAccounts accounts,
    LoDbDbContext db,
    BuildVersions versions,
    CatalogGateway gateway,
    BuildScores scores)
{
    public static void Map(IEndpointRouteBuilder share) =>
        share.MapGet(
                "/{token}",
                static (
                    [FromRoute] string token,
                    [AsParameters] ShareQuery query,
                    [FromServices] ShareEndpoint endpoint) => endpoint.ShowAsync(token, query))
            .WithName("getSharedBuild")
            .WithSummary("A build by the token of its link, rendered on its own patch.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<SharedBuild>, BuildProblem, CatalogProblem>> ShowAsync(
        string token,
        ShareQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Malformed is { } malformed)
        {
            return malformed;
        }

        var aborted = query.Context.RequestAborted;
        var build = ShareTokens.IsWellFormed(token) ? await FindAsync(token, aborted) : null;
        if (build?.Owner is null)
        {
            return BuildProblem.NotFound();
        }

        var current = query.Version ?? (await versions.LatestAsync(aborted))?.Value;
        var scene = await SceneAsync(build, query, current);
        var vote = build.IsPublic
            ? await scores.StateAsync(build.Id, accounts.IdOf(query.Context.User), aborted)
            : null;
        return TypedResults.Ok(SharedBuild.Of(build, scene, current) with { Vote = vote });
    }

    private Task<Build?> FindAsync(string token, CancellationToken cancellationToken) =>
        db.Builds.AsNoTracking()
            .Include(static build => build.Owner)
            .SingleOrDefaultAsync(build => build.ShareToken == token, cancellationToken);

    // A patch not readable now leaves every entry a ghost rather than failing the link.
    private async Task<BuildScene> SceneAsync(Build build, ShareQuery query, string? current)
    {
        var version = string.IsNullOrWhiteSpace(build.GameVersion) ? current : build.GameVersion;
        var scope = new CatalogScope(version, query.LanguageFor(build.Language));
        var aborted = query.Context.RequestAborted;
        var read = version is null ? null : await gateway.ReadAsync(scope, aborted);
        return await BuildScene.OpenAsync(
            read?.Context,
            [StoredStructures.Normalized(build)],
            aborted);
    }
}
