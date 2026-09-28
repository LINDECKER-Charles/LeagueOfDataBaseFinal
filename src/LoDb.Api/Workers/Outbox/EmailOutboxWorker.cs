using LoDb.Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Outbox;

/// <summary>
/// Sends the queued e-mails continuously (ADR 0003): batch after batch while some are due,
/// then a look every <c>LoDb:Outbox:PollInterval</c>.
/// </summary>
/// <remarks>
/// Every instance runs it: the batches never overlap (<see cref="IOutboxDispatcher"/>).
/// Without a relay (<c>LoDb:Mail:Host</c> empty) it says so once and stops; the messages
/// wait in the table.
/// </remarks>
internal sealed partial class EmailOutboxWorker(
    IOutboxDispatcher dispatcher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!dispatcher.IsSendingEnabled)
        {
            LogSendingDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var summary = await dispatcher.DispatchAsync(stoppingToken);
            if (!summary.BatchWasFull)
            {
                await Task.Delay(options.Value.PollInterval, timeProvider, stoppingToken);
            }
        }
    }

    [LoggerMessage(
        EventName = "outbox.sending.disabled",
        Level = LogLevel.Warning,
        Message = "No mail relay is configured (LoDb:Mail:Host): queued e-mails are not sent.")]
    private static partial void LogSendingDisabled(ILogger logger);
}
