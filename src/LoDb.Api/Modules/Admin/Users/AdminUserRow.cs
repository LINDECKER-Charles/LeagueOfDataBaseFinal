namespace LoDb.Api.Modules.Admin.Users;

/// <summary>An account in the moderation list.</summary>
internal sealed record AdminUserRow
{
    public required int Id { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required bool EmailVerified { get; init; }

    public required bool IsBanned { get; init; }

    public DateTimeOffset? BannedAt { get; init; }

    public string? BanReason { get; init; }

    public required bool IsSupporter { get; init; }

    public required bool IsPublicProfile { get; init; }

    /// <summary>Whether the account signs in with Google.</summary>
    public required bool Google { get; init; }

    public required bool IsAdmin { get; init; }

    public required bool TwoFactorEnabled { get; init; }

    public required int BuildCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
