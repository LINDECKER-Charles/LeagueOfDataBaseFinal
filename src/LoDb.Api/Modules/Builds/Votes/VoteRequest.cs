namespace LoDb.Api.Modules.Builds.Votes;

/// <summary>A vote on a public build.</summary>
internal sealed record VoteRequest
{
    /// <summary>
    /// <c>up</c> or <c>down</c>; the direction the caller already voted withdraws the vote.
    /// </summary>
    public string? Value { get; init; }
}
