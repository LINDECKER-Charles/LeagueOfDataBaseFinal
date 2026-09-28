using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Egress;

/// <summary>
/// Counts the refusals and failures of its fetches and reports them once, on disposal.
/// </summary>
internal sealed class EgressBatch(EgressFetcher fetcher, ILogger logger) : IEgressBatch
{
    // Enough to tell a caller bug from a probe, small enough to stay one line.
    private const int MaxLoggedHosts = 5;

    private readonly Lock gate = new();
    private readonly List<string> hosts = [];
    private int requested;
    private int refused;
    private int redirected;
    private int failed;
    private bool reported;

    public async Task<FetchOutcome> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            requested++;
        }

        try
        {
            return await fetcher.SendAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (EgressRefusedException refusal)
        {
            Record(refusal);
            throw;
        }
        catch (EgressException)
        {
            lock (gate)
            {
                failed++;
            }

            throw;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (reported)
            {
                return;
            }

            reported = true;
            Report();
        }
    }

    private void Record(EgressRefusedException refusal)
    {
        lock (gate)
        {
            if (refusal.Reason == EgressRefusal.Redirect)
            {
                redirected++;
                return;
            }

            refused++;
            if (refusal.Host is { } host && hosts.Count < MaxLoggedHosts && !hosts.Contains(host))
            {
                hosts.Add(host);
            }
        }
    }

    private void Report()
    {
        if (refused > 0)
        {
            EgressLog.AllowListRefused(logger, refused, requested, string.Join(',', hosts));
        }

        if (redirected > 0)
        {
            EgressLog.RedirectRefused(logger, redirected, requested);
        }

        if (failed > 0)
        {
            EgressLog.BatchDegraded(logger, failed, requested);
        }
    }
}
