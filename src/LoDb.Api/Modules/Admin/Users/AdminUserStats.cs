namespace LoDb.Api.Modules.Admin.Users;

/// <summary>The counters above the moderation list, over every account.</summary>
internal sealed record AdminUserStats
{
    public required int Total { get; init; }

    /// <summary>Accounts created over the last seven days.</summary>
    public required int NewWeek { get; init; }

    public required int Banned { get; init; }

    public required int Supporters { get; init; }
}
