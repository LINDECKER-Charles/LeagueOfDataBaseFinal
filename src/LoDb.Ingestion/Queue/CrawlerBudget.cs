using LoDb.Ingestion.Pipeline;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The on-demand requests crawlers may queue per minute on this instance (C5), in fixed
/// windows of one minute.
/// </summary>
internal sealed class CrawlerBudget(IOptions<IngestionOptions> options, TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private DateTimeOffset windowStart;
    private int taken;

    public bool TryTake()
    {
        var limit = options.Value.CrawlerRequestsPerMinute;
        if (limit == 0)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            if (now - windowStart >= Window)
            {
                windowStart = now;
                taken = 0;
            }

            if (taken >= limit)
            {
                return false;
            }

            taken++;
            return true;
        }
    }
}
