namespace LoDb.Api.Modules.Admin.Http;

/// <summary>An account a row points to, as the admin lists name it.</summary>
internal sealed record AdminUserRef
{
    public required int Id { get; init; }

    public required string Username { get; init; }

    /// <summary>Whether the account is banned now: the build list badges its author.</summary>
    public required bool IsBanned { get; init; }
}
