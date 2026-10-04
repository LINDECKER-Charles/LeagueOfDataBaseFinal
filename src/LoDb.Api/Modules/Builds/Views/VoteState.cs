namespace LoDb.Api.Modules.Builds.Views;

/// <summary>The votes on a public build: the net score only, and the caller's own vote.</summary>
internal sealed record VoteState
{
    /// <summary>Up votes minus down votes.</summary>
    public required int Score { get; init; }

    /// <summary>1 or -1 for the caller's vote; 0 without one, or for a visitor.</summary>
    public required int MyVote { get; init; }
}
