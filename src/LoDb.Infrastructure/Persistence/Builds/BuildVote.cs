using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Infrastructure.Persistence.Builds;

/// <summary>A row of <c>build_votes</c>: one vote per player and build.</summary>
public sealed class BuildVote
{
    public int Id { get; set; }

    /// <summary>+1 or -1.</summary>
    public short Value { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int BuildId { get; set; }

    public Build? Build { get; set; }

    public int VoterId { get; set; }

    public User? Voter { get; set; }
}
