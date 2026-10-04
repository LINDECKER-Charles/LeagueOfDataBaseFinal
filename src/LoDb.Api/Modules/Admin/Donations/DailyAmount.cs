namespace LoDb.Api.Modules.Admin.Donations;

/// <summary>What was given on one UTC day.</summary>
internal sealed record DailyAmount
{
    public required DateOnly Date { get; init; }

    public required long Cents { get; init; }
}
