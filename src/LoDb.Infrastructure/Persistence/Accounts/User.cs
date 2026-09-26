namespace LoDb.Infrastructure.Persistence.Accounts;

/// <summary>
/// A row of <c>users</c>, shared with the legacy stack until the cutover.
/// </summary>
/// <remarks>
/// Properties follow the Doctrine column order. The e-mail and the username are unique
/// whatever their case, through the <c>LOWER()</c> indexes of the baseline.
/// </remarks>
public sealed class User
{
    public int Id { get; set; }

    /// <summary>Stored lowercase by the legacy stack.</summary>
    public required string Email { get; set; }

    public required string Username { get; set; }

    /// <summary>Symfony roles, a JSON array such as <c>["ROLE_USER"]</c>.</summary>
    public required IReadOnlyList<string> Roles { get; set; }

    /// <summary>bcrypt or argon2 hash; null for an account created through Google.</summary>
    public string? Password { get; set; }

    public bool IsPublicProfile { get; set; }

    public string? FavoriteChampionId { get; set; }

    public string? FavoriteItemId { get; set; }

    public string? FavoriteRuneId { get; set; }

    public string? FavoriteSummonerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? GoogleId { get; set; }

    public string? RiotTagline { get; set; }

    public bool IsSupporter { get; set; }

    public bool IsBanned { get; set; }

    public DateTimeOffset? BannedAt { get; set; }

    public string? BanReason { get; set; }

    public bool IsVerified { get; set; }

    public string? FavoriteSkinId { get; set; }

    public string? PreferredVersion { get; set; }
}
