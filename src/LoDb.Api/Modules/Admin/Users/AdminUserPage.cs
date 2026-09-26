namespace LoDb.Api.Modules.Admin.Users;

/// <summary>A page of the accounts matching a search, newest first.</summary>
internal sealed record AdminUserPage
{
    public required AdminUserStats Stats { get; init; }

    public required IReadOnlyList<AdminUserRow> Items { get; init; }

    /// <summary>Accounts matching the search.</summary>
    public required int Total { get; init; }

    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    public required int Pages { get; init; }
}
