using LoDb.Api.Hosting;
using LoDb.Api.Modules.Builds.Catalogs;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Builds.Import;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Builds.Import;

/// <summary>
/// <c>GET /api/builds/{id}/import?to=</c>: a build of the signed-in account carried over to
/// another patch as an unsaved draft, with the report of what that patch no longer offers.
/// </summary>
/// <remarks>
/// Nothing is written: the author reviews the draft in the editor, which creates a new
/// build; the source stays pinned to its own patch.
/// </remarks>
internal sealed class ImportPreviewEndpoint(
    BuildAccounts accounts,
    OwnedBuilds owned,
    BuildVersions versions,
    CatalogGateway gateway)
{
    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapGet(
                "/{id:int}/import",
                static (
                    [FromRoute] int id,
                    [AsParameters] ImportQuery query,
                    [FromServices] ImportPreviewEndpoint endpoint) =>
                    endpoint.PreviewAsync(id, query))
            .RequireAuthorization(AuthorizationPolicies.VerifiedEmail)
            .WithName("previewBuildImport")
            .WithSummary("A build carried over to another patch as a draft, and what it lost.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<Ok<ImportPreview>, BuildProblem, CatalogProblem>> PreviewAsync(
        int id,
        ImportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var context = query.Context;
        if (await accounts.FindAsync(context.User) is not { } owner)
        {
            return BuildProblem.SignedOut();
        }

        var aborted = context.RequestAborted;
        if (await owned.FindAsync(id, owner.Id, aborted) is not { } build)
        {
            return BuildProblem.NotFound();
        }

        var read = await ReadTargetAsync(query, aborted);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var catalog = read.Context.Catalog;
        var projection = BuildStructureProjector.Project(
            StoredStructures.Read(build),
            StoredMode.Of(build),
            BuildCatalogs.Of(catalog));
        return TypedResults.Ok(ImportPreview.Of(build, catalog.Version.Value, projection));
    }

    private async Task<CatalogRead> ReadTargetAsync(
        ImportQuery query,
        CancellationToken cancellationToken)
    {
        var to = string.IsNullOrWhiteSpace(query.To)
            ? (await versions.LatestAsync(cancellationToken))?.Value
            : query.To.Trim();
        return to is null
            ? CatalogRead.Fail(CatalogProblem.UpstreamUnavailable())
            : await gateway.ReadAsync(new CatalogScope(to, query.Language), cancellationToken);
    }
}
