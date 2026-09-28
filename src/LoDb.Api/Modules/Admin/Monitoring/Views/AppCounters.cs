namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>What the community produced, and what the public API served.</summary>
internal sealed record AppCounters
{
    public required int UsersTotal { get; init; }

    /// <summary>Accounts created over the last seven days.</summary>
    public required int UsersNewWeek { get; init; }

    public required int UsersBanned { get; init; }

    public required int BuildsTotal { get; init; }

    public required int BuildsPublic { get; init; }

    public required int Votes { get; init; }

    public required int DonationsCount { get; init; }

    public required long DonationsTotalCents { get; init; }

    public required int ApiKeysActive { get; init; }

    /// <summary>Requests of the public API on the current UTC day.</summary>
    public required long ApiRequestsToday { get; init; }

    /// <summary>Requests of the public API since the first day of the UTC month.</summary>
    public required long ApiRequestsMonth { get; init; }
}
