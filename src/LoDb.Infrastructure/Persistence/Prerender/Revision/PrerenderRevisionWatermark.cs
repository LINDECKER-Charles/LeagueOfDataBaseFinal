namespace LoDb.Infrastructure.Persistence.Prerender.Revision;

/// <summary>
/// The single row of <c>prerender_revision_watermark</c>: how far the producer of
/// <c>prerender_revision</c> went through the closures of the index, so that a promotion
/// listener and a reconciliation, on any instance, enqueue each closure once.
/// </summary>
/// <remarks>
/// Absent until the producer first runs. Tied to a generation of the index: a rebuild's
/// generation dates every closure it makes by the rebuild, which the producer must not take
/// for that many promotions.
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

    public DateTimeOffset UpdatedAt { get; set; }
}
