namespace LoDb.Api.Modules.Admin.Scaleway.Views;

/// <summary>
/// The Object Storage bill of one Scaleway project: every bucket of the project together,
/// egress and requests included.
/// </summary>
internal sealed record ScalewayProjectBill
{
    public required Guid ProjectId { get; init; }

    /// <summary>The six months ending with the current one, newest first.</summary>
    public required IReadOnlyList<ScalewayBillPeriod> Periods { get; init; }
}
