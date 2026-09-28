using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>
/// Applies a verified event once: its row in <c>stripe_event</c> and its effects are written
/// in one transaction.
/// </summary>
/// <remarks>
/// The row is inserted first, <c>ON CONFLICT DO NOTHING</c>: nothing inserted means the event
/// is already applied. A redelivery running at the same time waits on the unique index until
/// the first one commits, then finds its row, or inserts it if the first one rolled back. A
/// handler that throws rolls the whole back, row included, so that the next delivery runs it
/// again. The cache and the journal are only told once the transaction commits.
/// </remarks>
internal sealed partial class WebhookProcessor(
    LoDbDbContext db,
    IEnumerable<IStripeEventHandler> handlers,
    BillingEffects effects,
    TimeProvider clock,
    ILogger<WebhookProcessor> logger)
{
    // The values of stripe_event.status.
    private const string ProcessedStatus = "processed";
    private const string IgnoredStatus = "ignored";

    public async Task<WebhookOutcome> ProcessAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        var handler = handlers.FirstOrDefault(
            candidate => candidate.EventType.Equals(stripeEvent.Type, StringComparison.Ordinal));
        try
        {
            if (!await ApplyAsync(stripeEvent, handler, cancellationToken))
            {
                LogDuplicate(logger, stripeEvent.Id, stripeEvent.Type);
                return WebhookOutcome.Duplicate;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            effects.Discard();
            LogFailed(logger, stripeEvent.Id, stripeEvent.Type, exception);
            return WebhookOutcome.Failed;
        }

        await effects.ApplyAsync();
        LogApplied(logger, stripeEvent.Id, stripeEvent.Type, handler is not null);
        return handler is null ? WebhookOutcome.Ignored : WebhookOutcome.Processed;
    }

    private async Task<bool> ApplyAsync(
        Event stripeEvent,
        IStripeEventHandler? handler,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var status = handler is null ? IgnoredStatus : ProcessedStatus;
        if (!await ClaimAsync(stripeEvent, status, cancellationToken))
        {
            return false;
        }

        if (handler is not null)
        {
            await handler.HandleAsync(stripeEvent, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> ClaimAsync(
        Event stripeEvent,
        string status,
        CancellationToken cancellationToken)
    {
        var createdAt = StripeTimes.CreatedAt(stripeEvent);
        var processedAt = clock.GetUtcNow();
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO stripe_event (id, type, status, created_at, processed_at)
            VALUES ({stripeEvent.Id}, {stripeEvent.Type}, {status}, {createdAt}, {processedAt})
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken);
        return inserted == 1;
    }

    [LoggerMessage(
        EventName = "billing.webhook.applied",
        Level = LogLevel.Information,
        Message = "Stripe event {EventId} of type {EventType} recorded (handled: {Handled}).")]
    private static partial void LogApplied(
        ILogger logger,
        string eventId,
        string eventType,
        bool handled);

    [LoggerMessage(
        EventName = "billing.webhook.duplicate",
        Level = LogLevel.Information,
        Message = "Stripe event {EventId} of type {EventType} already recorded: not applied.")]
    private static partial void LogDuplicate(ILogger logger, string eventId, string eventType);

    [LoggerMessage(
        EventName = "billing.webhook.failed",
        Level = LogLevel.Error,
        Message = "Stripe event {EventId} of type {EventType} failed and was rolled back.")]
    private static partial void LogFailed(
        ILogger logger,
        string eventId,
        string eventType,
        Exception exception);
}
