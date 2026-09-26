using LoDb.Domain.Builds.Votes;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>
/// Applies a vote to the one row a voter keeps per build: cast, switched, or withdrawn by the
/// same direction again.
/// </summary>
internal sealed class BallotBox(LoDbDbContext db)
{
    private const string VoterIndex = "uniq_build_votes_build_voter";

    /// <remarks>
    /// Two first votes of one voter racing (a double click) meet at the unique index: the
    /// loser applies again over the winner's row, as if it had come second.
    /// </remarks>
    public async Task CastAsync(VoteBallot ballot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ballot);
        try
        {
            await ApplyAsync(ballot, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            db.ChangeTracker.Clear();
            await ApplyAsync(ballot, cancellationToken);
        }
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: VoterIndex,
        };

    private async Task ApplyAsync(VoteBallot ballot, CancellationToken cancellationToken)
    {
        var standing = await db.BuildVotes.SingleOrDefaultAsync(
            vote => vote.BuildId == ballot.BuildId && vote.VoterId == ballot.VoterId,
            cancellationToken);
        if (standing is null)
        {
            db.BuildVotes.Add(ballot.ToVote());
        }
        else if (VoteRules.Toggle(standing.Value, ballot.Cast) is var value
            && value == VoteRules.None)
        {
            db.BuildVotes.Remove(standing);
        }
        else
        {
            standing.Value = (short)value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
