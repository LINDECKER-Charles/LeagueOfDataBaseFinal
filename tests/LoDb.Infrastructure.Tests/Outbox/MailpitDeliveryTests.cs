using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Outbox;
using LoDb.Infrastructure.Tests.Outbox.Support;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// One real delivery through MailKit, over an authenticated session: each queued message
/// reaches Mailpit in its recipient's locale, from the configured sender, with its recipient,
/// its Reply-To and both parts.
/// </summary>
/// <remarks>
/// What the dispatcher decides (retries, backoff, dead messages) and what the renderer writes
/// (subjects, locales) are tested without a relay, in <see cref="OutboxDispatcherTests"/> and
/// <see cref="EmailRendererTests"/>; only MailKit's session and the message it composes need
/// a real one.
/// </remarks>
public sealed class MailpitDeliveryTests(
    PostgresContainerFixture postgres,
    MailpitContainer mailpit) : MigratedDatabase(postgres), IClassFixture<MailpitContainer>
{
    private const string Player = "french.player@example.com";
    private const string Team = "team@lodb.test";
    private const string Visitor = "visitor@example.com";

    private OutboxHarness? _harness;

    [Fact]
    public async Task QueuedMessagesReachTheRelayOverAnAuthenticatedSession()
    {
        _harness = new OutboxHarness(
            Database.AppConnectionString,
            new Dictionary<string, string?>(mailpit.Settings)
            {
                ["LoDb:Mail:Username"] = "relay-user",
                ["LoDb:Mail:Password"] = "relay-password",
                ["LoDb:Mail:From"] = "LoDb Contact <contact@lodb.test>",
            });
        await _harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.Fr, Player), Cancellation);
        await _harness.EnqueueAsync(OutboxHarness.Contact(Team, Visitor), Cancellation);

        var summary = await _harness.Dispatcher.DispatchAsync(Cancellation);

        Assert.Equal(2, summary.Sent);
        var confirmation = (await mailpit.MessageToAsync(Player, Cancellation))!.Value;
        Assert.Equal(
            "Confirmez votre adresse e-mail · LeagueOfDataBase",
            confirmation.GetProperty("Subject").GetString());
        Assert.Equal(
            ("LoDb Contact", "contact@lodb.test"),
            Mailbox(confirmation.GetProperty("From")));
        Assert.Equal(("Faker", Player), Mailbox(confirmation.GetProperty("To")[0]));
        Assert.Contains(
            OutboxHarness.ActionUrl,
            confirmation.GetProperty("Text").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "<html lang=\"fr\"",
            confirmation.GetProperty("HTML").GetString(),
            StringComparison.Ordinal);
        var contact = (await mailpit.MessageToAsync(Team, Cancellation))!.Value;
        Assert.Equal(("Visiteur", Visitor), Mailbox(contact.GetProperty("ReplyTo")[0]));
        Assert.All(
            await _harness.RowsAsync(Cancellation),
            static row => Assert.Equal(EmailOutboxStatus.Sent, row.Status));
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    private static (string? Name, string? Address) Mailbox(JsonElement mailbox) =>
        (mailbox.GetProperty("Name").GetString(), mailbox.GetProperty("Address").GetString());
}
