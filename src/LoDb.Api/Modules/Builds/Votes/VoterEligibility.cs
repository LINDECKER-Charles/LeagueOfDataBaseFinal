using LoDb.Api.Modules.Builds.Http;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>
/// Whether an account's vote may weigh on the public rankings: its e-mail is verified, which
/// the endpoint's policy checks, and it exists since <see cref="VoteOptions.MinimumAccountAge"/>.
/// </summary>
/// <remarks>
/// A batch of accounts made to push a build up would have to wait a day, and to verify an
/// address each, before any of their votes counts.
/// </remarks>
internal sealed class VoterEligibility(IOptions<VoteOptions> options, TimeProvider time)
{
    /// <summary>The code of a vote refused to an account still too recent.</summary>
    public const string TooRecentCode = "account-too-recent";

    public bool IsOldEnough(User voter)
    {
        ArgumentNullException.ThrowIfNull(voter);
        return voter.CreatedAt <= time.GetUtcNow() - options.Value.MinimumAccountAge;
    }

    /// <summary>403 for an account that may vote once it is old enough.</summary>
    public static BuildProblem TooRecent() => new()
    {
        Status = StatusCodes.Status403Forbidden,
        Code = TooRecentCode,
        Title = "The account is too recent to vote yet.",
    };
}
