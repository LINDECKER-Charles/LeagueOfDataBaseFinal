using LoDb.Api.Modules.Admin.Http;

namespace LoDb.Api.Modules.Admin.ApiClients.Views;

/// <summary>A key of the public API, as the admin reads it: never its secret.</summary>
internal sealed record AdminApiClientRow
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The first characters of the key, the only part ever shown.</summary>
    public required string KeyPrefix { get; init; }

    /// <summary>free, credits, monthly, monthly_plus, annual or annual_plus.</summary>
    public required string Plan { get; init; }

    public required int MonthlyQuota { get; init; }

    /// <summary>Requests counted since the first day of the UTC month.</summary>
    public required long UsedThisMonth { get; init; }

    public required long CreditsBalance { get; init; }

    public required int RateLimitPerMin { get; init; }

    public required bool IsActive { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required AdminUserRef Owner { get; init; }
}
