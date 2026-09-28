using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Outbox.Delivery;
using LoDb.Infrastructure.Persistence.Outbox;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// The worker's batches: a due message is sent once, a failure comes back after an
/// exponential backoff, a message is dead after its last attempt or at once when its model
/// is unusable, and a lease that runs out gives the message to the next batch.
/// </summary>
public sealed class OutboxDispatcherTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private readonly FakeMailTransport _transport = new();
    private readonly List<OutboxHarness> _harnesses = [];

    [Fact]
    public async Task DueMessageIsSentOnceAndMarkedSent()
    {
        var harness = NewHarness();
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.Fr), Cancellation);

        var first = await harness.Dispatcher.DispatchAsync(Cancellation);
        var second = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Equal(new DispatchSummary(1, 1, 0, 0, BatchWasFull: false), first);
        Assert.Equal(DispatchSummary.Empty, second);
        var sent = Assert.Single(_transport.Sent);
        Assert.Equal(OutboxHarness.Recipient, sent.To.Mailboxes.Single().Address);
        Assert.Equal("no-reply@leagueofdatabase.gg", sent.From.Mailboxes.Single().Address);
        Assert.StartsWith("Confirmez votre adresse e-mail", sent.Subject, StringComparison.Ordinal);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal(EmailOutboxStatus.Sent, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal(OutboxHarness.Start, row.SentAt);
        Assert.Null(row.LastErrorCode);
    }

    [Fact]
    public async Task FailureIsRetriedWithExponentialBackoff()
    {
        var harness = NewHarness();
        _transport.AlwaysFail = new IOException("connection reset by relay");
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);
        var waits = new List<TimeSpan>();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var summary = await harness.Dispatcher.DispatchAsync(Cancellation);
            var early = await harness.Dispatcher.DispatchAsync(Cancellation);
            Assert.Equal(1, summary.Retried);
            Assert.Equal(DispatchSummary.Empty, early);
            var row = Assert.Single(await harness.RowsAsync(Cancellation));
            Assert.Equal(("io", attempt), (row.LastErrorCode, row.Attempts));
            waits.Add(row.NextAttemptAt - harness.Time.GetUtcNow());
            harness.Time.Advance(waits[^1]);
        }

        Assert.Equal([30, 60, 120, 240], waits.Select(static wait => wait.TotalSeconds));
    }

    [Fact]
    public async Task RecoveredRelaySendsTheRetriedMessage()
    {
        var harness = NewHarness();
        _transport.Failures.Enqueue(new IOException("connection reset by relay"));
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);

        await harness.Dispatcher.DispatchAsync(Cancellation);
        harness.Time.Advance(TimeSpan.FromSeconds(30));
        var retry = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Equal(1, retry.Sent);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal(
            (EmailOutboxStatus.Sent, 2, null),
            (row.Status, row.Attempts, row.LastErrorCode));
    }

    [Fact]
    public async Task MessageIsDeadAfterItsLastAttemptAndNeverLogsTheAddress()
    {
        var harness = NewHarness(new() { ["LoDb:Outbox:MaxAttempts"] = "3" });
        var refusal = $"5.1.1 <{OutboxHarness.Recipient}>: Recipient address rejected";
        _transport.AlwaysFail = new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            MailboxAddress.Parse(OutboxHarness.Recipient),
            refusal);
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);

        var summaries = new List<DispatchSummary>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            summaries.Add(await harness.Dispatcher.DispatchAsync(Cancellation));
            harness.Time.Advance(TimeSpan.FromHours(1));
        }

        Assert.Equal([1, 1, 0, 0], summaries.Select(static summary => summary.Retried));
        Assert.Equal([0, 0, 1, 0], summaries.Select(static summary => summary.Dead));
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal((EmailOutboxStatus.Dead, 3), (row.Status, row.Attempts));
        Assert.Equal("smtp.550", row.LastErrorCode);
        Assert.Contains(harness.Logs, static log => log.Id.Name == "outbox.message.dead");
        Assert.DoesNotContain(harness.Logs, static log =>
            log.Message.Contains(OutboxHarness.Recipient, StringComparison.Ordinal)
            || log.Exception is not null
            || log.StructuredState!.Any(static value =>
                value.Value?.Contains('@', StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task UnusableModelIsDeadAtOnce()
    {
        var harness = NewHarness();
        var message = OutboxHarness.Confirmation(UiLocale.En) with
        {
            Model = new Dictionary<string, string?> { [EmailModelKeys.UserName] = "Faker" },
        };
        await harness.EnqueueAsync(message, Cancellation);

        var summary = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Equal(new DispatchSummary(1, 0, 0, 1, BatchWasFull: false), summary);
        Assert.Equal(0, _transport.Attempts);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal((EmailOutboxStatus.Dead, "model.invalid"), (row.Status, row.LastErrorCode));
    }

    [Fact]
    public async Task MessageOfADeadInstanceIsTakenAgainWhenItsLeaseEnds()
    {
        var harness = NewHarness();
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);

        // An instance takes the message, then dies before recording anything.
        var taken = await harness.Services.GetRequiredService<OutboxQueue>()
            .ClaimAsync(Cancellation);
        var duringLease = await harness.Dispatcher.DispatchAsync(Cancellation);
        harness.Time.Advance(new OutboxOptions().Lease);
        var afterLease = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Single(taken);
        Assert.Equal(DispatchSummary.Empty, duringLease);
        Assert.Equal(1, afterLease.Sent);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal((EmailOutboxStatus.Sent, 2), (row.Status, row.Attempts));
    }

    [Fact]
    public async Task InstancesSharingTheTableSendEachMessageOnce()
    {
        var settings = new Dictionary<string, string?> { ["LoDb:Outbox:BatchSize"] = "4" };
        var first = NewHarness(settings);
        var second = NewHarness(settings);
        for (var index = 0; index < 30; index++)
        {
            var recipient = $"player{index}@example.com";
            var message = OutboxHarness.Confirmation(UiLocale.En, recipient);
            await first.EnqueueAsync(message, Cancellation);
        }

        await Task.WhenAll(DrainAsync(first), DrainAsync(second));

        var recipients = _transport.Sent.Select(static sent => sent.To.Mailboxes.Single().Address);
        Assert.Equal(30, recipients.Count());
        Assert.Equal(30, recipients.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            await first.RowsAsync(Cancellation),
            static row => Assert.Equal((EmailOutboxStatus.Sent, 1), (row.Status, row.Attempts)));
    }

    [Fact]
    public async Task NoRelayMeansNothingIsTaken()
    {
        var harness = NewHarness(new() { ["LoDb:Mail:Host"] = string.Empty });
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);

        var summary = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.False(harness.Dispatcher.IsSendingEnabled);
        Assert.Equal(DispatchSummary.Empty, summary);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal((EmailOutboxStatus.Pending, 0), (row.Status, row.Attempts));
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        foreach (var harness in _harnesses)
        {
            await harness.DisposeAsync();
        }
    }

    private static async Task DrainAsync(OutboxHarness harness)
    {
        while ((await harness.Dispatcher.DispatchAsync(Cancellation)).Claimed > 0)
        {
        }
    }

    private OutboxHarness NewHarness(Dictionary<string, string?>? settings = null)
    {
        var harness = new OutboxHarness(Database.ConnectionString, settings, _transport);
        _harnesses.Add(harness);
        return harness;
    }
}
