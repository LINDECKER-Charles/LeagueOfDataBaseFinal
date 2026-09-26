using LoDb.Domain.Builds.Votes;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>A vote a caller sends on a build.</summary>
internal sealed record VoteBallot
{
    public required int BuildId { get; init; }

    public required int VoterId { get; init; }

    public required VoteDirection Cast { get; init; }

    public required DateTimeOffset CastAt { get; init; }

    /// <summary>The row of a first vote.</summary>
    public BuildVote ToVote() => new()
    {
        BuildId = BuildId,
        VoterId = VoterId,
        Value = (short)Cast,
        CreatedAt = CastAt,
    };
}
