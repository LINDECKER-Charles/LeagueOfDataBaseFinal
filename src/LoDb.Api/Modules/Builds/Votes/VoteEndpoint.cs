using LoDb.Api.Hosting;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Domain.Builds.Votes;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Builds;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>
/// <c>POST /api/builds/{id}/vote</c>: votes a public build up or down, or withdraws the vote;
/// a private build is a 404, as an unknown one.
/// </summary>
/// <remarks>
/// Only a verified account old enough (<see cref="VoterEligibility"/>) votes, within its
/// hourly limit (<see cref="BuildLimits.Vote"/>): the votes rank the public builds.
/// </remarks>
internal sealed class VoteEndpoint(
    BuildAccounts accounts,
    VoterEligibility eligibility,
    LoDbDbContext db,
    BallotBox ballots,
    BuildScores scores,
    BuildAudit audit,
    TimeProvider time)
{
    /// <summary>The code of a value other than <c>up</c> and <c>down</c>.</summary>
    public const string InvalidValue = "vote.invalid";

    public static void Map(IEndpointRouteBuilder builds) =>
        builds.MapPost(
                "/{id:int}/vote",
                static (
                    [FromRoute] int id,
                    [FromBody] VoteRequest request,
                    [FromServices] VoteEndpoint endpoint,
                    HttpContext context) => endpoint.VoteAsync(id, request, context))
            .RequireAuthorization(AuthorizationPolicies.VerifiedEmail)
            .RequireRateLimiting(BuildLimits.Vote.Policy)
            .WithName("voteBuild")
            .WithSummary("Votes a public build up or down; the same vote again withdraws it.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<Ok<VoteState>, BuildProblem>> VoteAsync(
        int id,
        VoteRequest request,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await accounts.FindAsync(context.User) is not { } voter)
        {
            return BuildProblem.SignedOut();
        }

        if (!eligibility.IsOldEnough(voter))
        {
            return VoterEligibility.TooRecent();
        }

        if (!VoteRules.TryParse(request.Value, out var cast))
        {
            return Invalid();
        }

        var aborted = context.RequestAborted;
        if (await FindPublicAsync(id, aborted) is not { } build)
        {
            return BuildProblem.NotFound();
        }

        await ballots.CastAsync(Ballot(build, voter.Id, cast), aborted);
        await audit.VotedAsync(build, cast, aborted);
        return TypedResults.Ok(await scores.StateAsync(build.Id, voter.Id, aborted));
    }

    private static BuildProblem Invalid()
    {
        var errors = new BuildFieldErrors();
        errors.Add(BuildFields.Value, InvalidValue);
        return errors.ToProblem();
    }

    private Task<Build?> FindPublicAsync(int id, CancellationToken cancellationToken) =>
        db.Builds.AsNoTracking()
            .SingleOrDefaultAsync(build => build.Id == id && build.IsPublic, cancellationToken);

    private VoteBallot Ballot(Build build, int voterId, VoteDirection cast) => new()
    {
        BuildId = build.Id,
        VoterId = voterId,
        Cast = cast,
        CastAt = StoredTime.Now(time),
    };
}
