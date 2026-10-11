namespace LoDb.Api.Modules.Accounts.Tokens;

/// <summary>What a refresh came to: the next link, or a refusal, a replay among them.</summary>
/// <param name="Next">The link of the token to hand out; null when refused.</param>
/// <param name="IsReplay">
/// Whether the token had been traded already, which revoked its family.
/// </param>
internal sealed record RefreshRotation(RefreshLink? Next, bool IsReplay)
{
    /// <summary>No such family, or a revoked one.</summary>
    public static RefreshRotation Refused { get; } = new(null, false);

    public static RefreshRotation Replayed { get; } = new(null, true);
}
