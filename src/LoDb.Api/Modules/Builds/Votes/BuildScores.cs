using System.Collections.Frozen;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Domain.Builds.Votes;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>The net scores of builds, and the votes a caller cast on them.</summary>
internal sealed class BuildScores(LoDbDbContext db)
{
    /// <summary>The net score of each build that has votes; the others score 0.</summary>
    public async Task<IReadOnlyDictionary<int, int>> ScoresAsync(
        IReadOnlyCollection<int> buildIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildIds);
        if (buildIds.Count == 0)
        {
            return FrozenDictionary<int, int>.Empty;
        }

        return await db.BuildVotes.AsNoTracking()
            .Where(vote => buildIds.Contains(vote.BuildId))
            .GroupBy(static vote => vote.BuildId)
            .Select(static votes => new { Id = votes.Key, Score = votes.Sum(v => (int)v.Value) })
            .ToDictionaryAsync(static row => row.Id, static row => row.Score, cancellationToken);
    }

    /// <summary>The vote <paramref name="voterId"/> cast on each build; none for nobody.</summary>
    public async Task<IReadOnlyDictionary<int, int>> VotesOfAsync(
        int? voterId,
        IReadOnlyCollection<int> buildIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildIds);
        if (voterId is not { } voter || buildIds.Count == 0)
        {
            return FrozenDictionary<int, int>.Empty;
        }

        return await db.BuildVotes.AsNoTracking()
            .Where(vote => vote.VoterId == voter && buildIds.Contains(vote.BuildId))
            .ToDictionaryAsync(
                static vote => vote.BuildId,
                static vote => (int)vote.Value,
                cancellationToken);
    }

    public async Task<VoteState> StateAsync(
        int buildId,
        int? voterId,
        CancellationToken cancellationToken)
    {
        int[] ids = [buildId];
        var scores = await ScoresAsync(ids, cancellationToken);
        var votes = await VotesOfAsync(voterId, ids, cancellationToken);
        return new VoteState
        {
            Score = scores.GetValueOrDefault(buildId),
            MyVote = votes.GetValueOrDefault(buildId, VoteRules.None),
        };
    }
}
