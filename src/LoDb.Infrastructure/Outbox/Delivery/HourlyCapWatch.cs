using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// Says in the logs when a template reaches its hourly cap and when it sends again: one line
/// each way, never one per look at the table; <c>lodb_outbox_deferred</c> counts between.
/// </summary>
/// <remarks>One per instance, as the dispatcher; its own state, guarded.</remarks>
internal sealed partial class HourlyCapWatch(ILogger<HourlyCapWatch> logger)
{
    private readonly Lock _gate = new();
    private HashSet<string> _capped = new(StringComparer.Ordinal);

    public void Observe(SendingBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        lock (_gate)
        {
            foreach (var template in budget.Capped.Where(template => !_capped.Contains(template)))
            {
                LogCapReached(logger, template, budget.HeldBack);
            }

            var current = budget.Capped.ToHashSet(StringComparer.Ordinal);
            foreach (var template in _capped.Where(template => !current.Contains(template)))
            {
                LogCapReleased(logger, template);
            }

            _capped = current;
        }
    }

    [LoggerMessage(
        EventName = "outbox.hourly_cap.reached",
        Level = LogLevel.Warning,
        Message = "Outbox template {Template} reached its hourly cap: {Deferred} due messages"
            + " deferred.")]
    private static partial void LogCapReached(ILogger logger, string template, long deferred);

    [LoggerMessage(
        EventName = "outbox.hourly_cap.released",
        Level = LogLevel.Information,
        Message = "Outbox template {Template} is under its hourly cap again.")]
    private static partial void LogCapReleased(ILogger logger, string template);
}
