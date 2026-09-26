using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Modules.Profiles.Reading;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary>
/// <c>PUT /api/profile/version</c>: pins the favorites to a version, or follows the version
/// browsed again.
/// </summary>
/// <remarks>
/// Only a version Data Dragon lists is stored, so that the favorites always resolve; the
/// legacy form cleared the pin on an unknown one, the API refuses it and keeps the pin.
/// </remarks>
internal sealed class PreferredVersionEndpoint(
    ProfileOwners owners,
    ProfileCatalog catalogs,
    LoDbDbContext db,
    ProfileAudit audit)
{
    public const string VersionInvalid = "version-invalid";
    public const string VersionUnknown = "version-unknown";

    // Length of users.preferred_version.
    private const int MaxVersionLength = 24;

    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapPut(
                "/version",
                static (
                    [FromBody] PreferredVersionRequest request,
                    [FromServices] PreferredVersionEndpoint endpoint,
                    HttpContext context) => endpoint.SetAsync(request, context))
            .WithName("setPreferredVersion")
            .WithSummary("Pins the favorites to a version, or unpins them.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<NoContent, AccountProblem, CatalogProblem>> SetAsync(
        PreferredVersionRequest request,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        var aborted = context.RequestAborted;
        var requested = request.Version?.Trim();
        if (!string.IsNullOrEmpty(requested) && await RefusalAsync(requested, aborted) is { } no)
        {
            return no;
        }

        owner.PreferredVersion = string.IsNullOrEmpty(requested) ? null : requested;
        await db.SaveChangesAsync(aborted);
        await audit.UpdatedAsync(ProfileAudit.PreferredVersionSection, aborted);
        return TypedResults.NoContent();
    }

    // Null when Data Dragon lists the version.
    private async Task<Results<NoContent, AccountProblem, CatalogProblem>?> RefusalAsync(
        string requested,
        CancellationToken cancellationToken)
    {
        if (!IsWellFormed(requested, out var version))
        {
            return Refused(VersionInvalid);
        }

        return await catalogs.IsListedAsync(version, cancellationToken) switch
        {
            null => CatalogProblem.UpstreamUnavailable(),
            false => Refused(VersionUnknown),
            true => null,
        };
    }

    private static bool IsWellFormed(
        string requested,
        [NotNullWhen(true)] out PatchVersion? version)
    {
        version = null;
        return requested.Length <= MaxVersionLength
            && PatchVersion.TryParse(requested, out version);
    }

    private static AccountProblem Refused(string code)
    {
        var errors = new FieldErrors();
        errors.Add(ProfileFields.Version, code);
        return errors.ToProblem();
    }
}
