namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>The queues of this instance and of the e-mail outbox.</summary>
internal sealed record IngestionState
{
    /// <summary>Versions waiting or being ingested on this instance.</summary>
    public required int VersionBacklog { get; init; }

    /// <summary>On-demand ingestions waiting on this instance.</summary>
    public required int OnDemandBacklog { get; init; }

    /// <summary>E-mails waiting for their first or next attempt; null when unreadable.</summary>
    public int? OutboxPending { get; init; }

    /// <summary>E-mails given up after their last attempt; null when unreadable.</summary>
    public int? OutboxDead { get; init; }
}
