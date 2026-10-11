namespace LoDb.Infrastructure.Persistence.Accounts;

/// <summary>
/// A row of <c>refresh_token_families</c>: the refresh tokens an app sign-in hands out, one
/// after the other. Each refresh replaces the token by the next generation's; a token of an
/// older generation coming back means two holders, and revokes the family.
/// </summary>
/// <remarks>
/// The tokens themselves are not stored: each one names its family and its generation. The
/// table is the new stack's alone, which the legacy stack never reads.
/// </remarks>
public sealed class RefreshTokenFamily
{
    public required Guid Id { get; init; }

    public required int UserId { get; init; }

    /// <summary>The generation of the one token of the family that refreshes now.</summary>
    public int Generation { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the last refresh moved the family to <see cref="Generation"/>.</summary>
    public DateTimeOffset RotatedAt { get; set; }

    /// <summary>When the latest token of the family runs out, and the row may go.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When the family was revoked: a sign-out, or a token played twice.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
