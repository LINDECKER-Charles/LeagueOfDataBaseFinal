namespace LoDb.Domain.Builds.Votes;

/// <summary>A vote on a public build, valued as the score sums it.</summary>
public enum VoteDirection
{
    Down = -1,
    Up = 1,
}
