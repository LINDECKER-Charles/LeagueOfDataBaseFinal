namespace LoDb.Api.Modules.PublicApi.Keys.Contracts;

/// <summary>The requests a key made on one UTC day.</summary>
internal sealed record ApiUsageDay
{
    public required DateOnly Day { get; init; }

    public required long Requests { get; init; }
}
