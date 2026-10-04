namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>Requests to add to the prepaid balance of a key.</summary>
internal sealed record CreditRequest
{
    /// <summary>From 1 to 1,000,000.</summary>
    public long? Requests { get; init; }
}
