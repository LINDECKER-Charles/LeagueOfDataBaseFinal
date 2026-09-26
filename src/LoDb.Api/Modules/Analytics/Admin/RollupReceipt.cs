namespace LoDb.Api.Modules.Analytics.Admin;

/// <summary>What an on-demand rollup wrote.</summary>
internal sealed record RollupReceipt
{
    /// <summary>The days whose aggregate was written, in order, today last if it was.</summary>
    public required IReadOnlyList<DateOnly> Days { get; init; }
}
