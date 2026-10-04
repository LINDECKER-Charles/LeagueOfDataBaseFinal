namespace LoDb.Infrastructure.Persistence.PublicApi;

/// <summary>
/// A row of <c>api_usage</c>: requests of one key on one UTC day, counted by an upsert on
/// (<c>api_key_id</c>, <c>day</c>).
/// </summary>
public sealed class ApiUsage
{
    public long Id { get; set; }

    public DateOnly Day { get; set; }

    public long Requests { get; set; }

    public int ApiKeyId { get; set; }

    public ApiKey? ApiKey { get; set; }
}
