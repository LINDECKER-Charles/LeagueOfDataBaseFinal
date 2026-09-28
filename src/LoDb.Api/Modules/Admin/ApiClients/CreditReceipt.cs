namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>The balance and rate of a key once credited.</summary>
internal sealed record CreditReceipt
{
    public required long CreditsBalance { get; init; }

    /// <summary>Raised to the floor of the keys holding credits when it was below.</summary>
    public required int RateLimitPerMin { get; init; }
}
