namespace LoDb.Infrastructure.Persistence.Prerender.Revision;

/// <summary>
/// The single row of <c>prerender_revision_watermark</c>: how far the producer of
/// <c>prerender_revision</c> went through the closures of the index, so that a promotion
/// listener and a reconciliation, on any instance, enqueue each closure once.
/// </summary>
/// <remarks>
/// Absent until the producer first runs. Tied to a generation of the index: a rebuild's
/// generation dates every closure it makes by the rebuild, which the producer must not take
/// for that many promotions. What it must take is told by the coverage the producer last read:
/// a closure past it was never enqueued, whatever its date.
/// </remarks>
public sealed class PrerenderRevisionWatermark
{
    /// <summary>The only key the table accepts.</summary>
    public const short SingletonId = 1;

    public short Id { get; set; } = SingletonId;

    /// <summary>The generation of <c>catalog_state_generation</c> the watermark reads.</summary>
    public long GenerationId { get; set; }

    /// <summary>The <c>closed_at</c> of the last closure enqueued.</summary>
    public DateTimeOffset ClosedThrough { get; set; }

    /// <summary>
    /// The last version the index covered when the producer last ran: every closure it had
    /// then ends before it. Null for a row written before the column.
    /// </summary>
    public string? CoveredThrough { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
