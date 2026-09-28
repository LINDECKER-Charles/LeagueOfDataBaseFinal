using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Outbox;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// Real deliveries through MailKit: a queued message reaches Mailpit in its recipient's
/// locale, with its sender, recipient, Reply-To and both parts; an unreachable relay makes
/// the message wait for its next attempt.
/// </summary>
public sealed class MailpitDeliveryTests(
    PostgresContainerFixture postgres,
    MailpitContainer mailpit) : MigratedDatabase(postgres), IClassFixture<MailpitContainer>
{
    private readonly List<OutboxHarness> _harnesses = [];

    [Fact]
    public async Task ConfirmationArrivesInTheRecipientsLocale()
    {
        var harness = NewHarness(mailpit.Settings);
        const string recipient = "french.player@example.com";
        var queued = OutboxHarness.Confirmation(UiLocale.Fr, recipient);
        await harness.EnqueueAsync(queued, Cancellation);

        var summary = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Equal(1, summary.Sent);
        var message = (await mailpit.MessageToAsync(recipient, Cancellation))!.Value;
        Assert.Equal(
            "Confirmez votre adresse e-mail · LeagueOfDataBase",
            message.GetProperty("Subject").GetString());
        Assert.Equal(
            ("LeagueOfDataBase", "no-reply@leagueofdatabase.gg"),
            Mailbox(message.GetProperty("From")));
        Assert.Equal(("Faker", recipient), Mailbox(message.GetProperty("To")[0]));
        Assert.Contains(
            OutboxHarness.ActionUrl,
            message.GetProperty("Text").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "<html lang=\"fr\"",
            message.GetProperty("HTML").GetString(),
            StringComparison.Ordinal);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal(EmailOutboxStatus.Sent, row.Status);
    }

    [Fact]
    public async Task ContactNotificationRepliesToTheVisitorOverAnAuthenticatedSession()
    {
        var settings = new Dictionary<string, string?>(mailpit.Settings)
        {
            ["LoDb:Mail:Username"] = "relay-user",
            ["LoDb:Mail:Password"] = "relay-password",
            ["LoDb:Mail:From"] = "LoDb Contact <contact@lodb.test>",
        };
        var harness = NewHarness(settings);
        const string team = "team@lodb.test";
        var queued = OutboxHarness.Contact(team, "visitor@example.com");
        await harness.EnqueueAsync(queued, Cancellation);

        await harness.Dispatcher.DispatchAsync(Cancellation);

        var message = (await mailpit.MessageToAsync(team, Cancellation))!.Value;
        Assert.Equal(
            "[Contact · Bug / anomalie] Page blanche",
            message.GetProperty("Subject").GetString());
        Assert.Equal(("LoDb Contact", "contact@lodb.test"), Mailbox(message.GetProperty("From")));
        Assert.Equal(
            ("Visiteur", "visitor@example.com"),
            Mailbox(message.GetProperty("ReplyTo")[0]));
    }

    [Fact]
    public async Task UnreachableRelayLeavesTheMessageForItsNextAttempt()
    {
        // Port 9 (discard) is closed on the test host: the connection is refused at once.
        var harness = NewHarness(new Dictionary<string, string?>
        {
            ["LoDb:Mail:Host"] = "127.0.0.1",
            ["LoDb:Mail:Port"] = "9",
            ["LoDb:Mail:Security"] = "None",
        });
        await harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);

        var summary = await harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Equal(1, summary.Retried);
        var row = Assert.Single(await harness.RowsAsync(Cancellation));
        Assert.Equal((EmailOutboxStatus.Pending, 1), (row.Status, row.Attempts));
        Assert.Equal("socket.ConnectionRefused", row.LastErrorCode);
        Assert.Equal(OutboxHarness.Start + new OutboxOptions().RetryDelay, row.NextAttemptAt);
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        foreach (var harness in _harnesses)
        {
            await harness.DisposeAsync();
        }
    }

    private static (string? Name, string? Address) Mailbox(JsonElement mailbox) =>
        (mailbox.GetProperty("Name").GetString(), mailbox.GetProperty("Address").GetString());

    private OutboxHarness NewHarness(IReadOnlyDictionary<string, string?> settings)
    {
        var harness = new OutboxHarness(Database.ConnectionString, settings);
        _harnesses.Add(harness);
        return harness;
    }
}
