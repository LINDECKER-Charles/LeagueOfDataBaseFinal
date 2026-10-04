using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox.Rendering;
using LoDb.Infrastructure.Outbox.Smtp;
using LoDb.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// One batch: take the due messages, write each in its locale, send them on one session of
/// the relay, then record each outcome — sent, due again after a backoff, or dead.
/// </summary>
/// <remarks>
/// Delivery is at least once: an instance that dies between the relay's answer and the
/// write of the outcome leaves the message to be sent again when its lease is over. The
/// logs name the message by id and template, never by address.
/// </remarks>
internal sealed partial class OutboxDispatcher(
    OutboxQueue queue,
    IMailTransport transport,
    EmailComposer composer,
    OutboxMetrics metrics,
    IOptions<MailOptions> mail,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private enum Outcome
    {
        Sent,
        Retried,
        Dead,
    }

    public bool IsSendingEnabled => mail.Value.IsEnabled;

    public async Task<DispatchSummary> DispatchAsync(CancellationToken cancellationToken)
    {
        if (!IsSendingEnabled)
        {
            return DispatchSummary.Empty;
        }

        var started = timeProvider.GetTimestamp();
        try
        {
            var claimed = await queue.ClaimAsync(cancellationToken);
            if (claimed.Count == 0)
            {
                metrics.RecordPending(await queue.CountPendingAsync(cancellationToken));
                return DispatchSummary.Empty;
            }

            var outcomes = await DeliverAsync(claimed, cancellationToken);
            var pending = await queue.CountPendingAsync(cancellationToken);
            return Summarize(outcomes, pending, timeProvider.GetElapsedTime(started));
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            metrics.RecordFailure();
            LogBatchFailed(logger, exception);
            return DispatchSummary.Empty;
        }
    }

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private static (EmailTemplate Template, UiLocale Locale) Stored(ClaimedMessage message)
    {
        try
        {
            var template = OutboxColumns.ParseTemplate(message.Template);
            return UiLocales.TryParse(message.Locale, out var locale)
                ? (template, locale)
                : throw new UnknownStoredValueException("Unknown stored locale.");
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new UnknownStoredValueException("Unknown stored template.");
        }
    }

    private async Task<List<Outcome>> DeliverAsync(
        IReadOnlyList<ClaimedMessage> claimed,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<Outcome>(claimed.Count);
        await using var session = transport.OpenSession();
        foreach (var message in claimed)
        {
            outcomes.Add(await DeliverAsync(session, message, cancellationToken));
        }

        return outcomes;
    }

    private async Task<Outcome> DeliverAsync(
        IMailSession session,
        ClaimedMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await session.SendAsync(Compose(message), cancellationToken);
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            return await RecordFailureAsync(message, exception, cancellationToken);
        }

        await queue.MarkSentAsync(message, cancellationToken);
        return Outcome.Sent;
    }

    private MimeMessage Compose(ClaimedMessage message)
    {
        var (template, locale) = Stored(message);
        var email = EmailRenderer.Render(template, locale, message.Model);
        return composer.Compose(message.Recipient, email);
    }

    private async Task<Outcome> RecordFailureAsync(
        ClaimedMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var code = DeliveryFailure.Code(exception);
        var dead = DeliveryFailure.IsPermanent(exception)
            || RetrySchedule.IsLast(message.Attempts, options.Value);
        var delay = dead ? TimeSpan.Zero : RetrySchedule.Delay(message.Attempts, options.Value);
        var attempt = new FailedAttempt(code, dead, timeProvider.GetUtcNow() + delay);
        await queue.MarkFailedAsync(message, attempt, cancellationToken);
        if (dead)
        {
            LogDead(logger, message.Id, message.Template, message.Attempts, code);
            return Outcome.Dead;
        }

        LogRetried(
            logger,
            message.Id,
            message.Template,
            message.Attempts,
            code,
            (long)delay.TotalSeconds);
        return Outcome.Retried;
    }

    private DispatchSummary Summarize(List<Outcome> outcomes, long pending, TimeSpan duration)
    {
        var sent = outcomes.Count(static outcome => outcome == Outcome.Sent);
        var retried = outcomes.Count(static outcome => outcome == Outcome.Retried);
        var dead = outcomes.Count(static outcome => outcome == Outcome.Dead);
        metrics.RecordMessages("sent", sent);
        metrics.RecordMessages("retried", retried);
        metrics.RecordMessages("dead", dead);
        metrics.RecordBatch(duration);
        metrics.RecordPending(pending);
        var durationMs = (long)duration.TotalMilliseconds;
        LogBatch(logger, outcomes.Count, sent, retried, dead, pending, durationMs);
        return new DispatchSummary(
            outcomes.Count,
            sent,
            retried,
            dead,
            BatchWasFull: outcomes.Count >= options.Value.BatchSize);
    }

    [LoggerMessage(
        EventName = "outbox.batch.completed",
        Level = LogLevel.Information,
        Message = "Outbox took {Claimed} messages: {Sent} sent, {Retried} retried, {Dead} dead"
            + " in {DurationMs} ms; {Pending} pending.")]
    private static partial void LogBatch(
        ILogger logger,
        int claimed,
        int sent,
        int retried,
        int dead,
        long pending,
        long durationMs);

    [LoggerMessage(
        EventName = "outbox.batch.failed",
        Level = LogLevel.Error,
        Message = "Outbox batch failed; its messages are taken again when their lease ends.")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "outbox.message.retried",
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} ({Template}) failed attempt {Attempt} with"
            + " {ErrorCode}; next attempt in {DelaySeconds} s.")]
    private static partial void LogRetried(
        ILogger logger,
        long messageId,
        string template,
        int attempt,
        string errorCode,
        long delaySeconds);

    [LoggerMessage(
        EventName = "outbox.message.dead",
        Level = LogLevel.Error,
        Message = "Outbox message {MessageId} ({Template}) is dead after attempt {Attempt}:"
            + " {ErrorCode}.")]
    private static partial void LogDead(
        ILogger logger,
        long messageId,
        string template,
        int attempt,
        string errorCode);
}
