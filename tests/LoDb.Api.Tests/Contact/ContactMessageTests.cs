using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Contact.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;

namespace LoDb.Api.Tests.Contact;

/// <summary>
/// <c>POST /api/contact</c>: the message is kept for the admin inbox and its notification
/// queued for the team, the visitor as Reply-To; without a mailbox it is only kept, and a
/// filled honeypot or a foreign origin keeps nothing.
/// </summary>
public sealed class ContactMessageTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private ContactApp? _app;

    private ContactApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task MessageIsKeptAndItsNotificationQueued()
    {
        using var browser = App.Browser();
        var sentAt = App.Clock.GetUtcNow();
        using var request = browser.Post(ContactForm.Path, ContactForm.Valid());
        request.Headers.Add("X-Forwarded-For", "203.0.113.5");

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = Assert.Single(await App.MessagesAsync());
        Assert.Equal(
            ("bug", "Ahri Fan", "visiteur@example.test", "Runes", ContactForm.Message),
            (message.Category, message.Name, message.Email, message.Subject, message.Message));
        Assert.Equal(("fr", "new", (int?)null), (message.Locale, message.Status, message.UserId));
        // The column keeps whole seconds, as the legacy one did.
        Assert.Equal(sentAt, message.CreatedAt, TimeSpan.FromSeconds(1));
        Assert.Equal("203.0.113.5", message.Ip);
        var mail = Assert.Single(App.Outbox.Messages);
        Assert.Equal(
            (ContactApp.Recipient, EmailTemplate.ContactNotification, UiLocale.Fr),
            (mail.Recipient, mail.Template, mail.Locale));
        Assert.Equal("visiteur@example.test", mail.Model[EmailModelKeys.ContactEmail]);
        Assert.Equal("bug", mail.Model[EmailModelKeys.ContactCategory]);
        Assert.Equal("Ahri Fan", mail.Model[EmailModelKeys.ContactName]);
        Assert.Equal("Runes", mail.Model[EmailModelKeys.ContactSubject]);
        Assert.Equal(ContactForm.Message, mail.Model[EmailModelKeys.ContactMessage]);
        Assert.Equal("fr", mail.Model[EmailModelKeys.ContactLocale]);
        Assert.Equal(
            sentAt,
            DateTimeOffset.Parse(
                mail.Model[EmailModelKeys.ContactReceivedAt]!,
                CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task FieldsAreTrimmedAndBlankOrUnknownOnesDropped()
    {
        using var browser = App.Browser();
        var body = ContactForm.Valid();
        body["category"] = "commercial";
        body["name"] = "   ";
        body["subject"] = null;
        body["email"] = "  visiteur@example.test ";
        body["message"] = $"\n  {ContactForm.Message}  \n";
        body["locale"] = "xx";

        using var response = await browser.PostAsync(ContactForm.Path, body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = Assert.Single(await App.MessagesAsync());
        Assert.Equal(
            ("commercial", (string?)null, "visiteur@example.test", (string?)null),
            (message.Category, message.Name, message.Email, message.Subject));
        Assert.Equal((ContactForm.Message, (string?)null), (message.Message, message.Locale));
        var mail = Assert.Single(App.Outbox.Messages);
        Assert.Null(mail.Model[EmailModelKeys.ContactName]);
        Assert.Null(mail.Model[EmailModelKeys.ContactSubject]);
    }

    [Fact]
    public async Task WithoutARecipientTheMessageIsOnlyKept()
    {
        await using var app = await ContactApp.StartAsync(postgres, recipient: "  ");
        using var browser = app.Browser();

        using var response = await browser.PostAsync(ContactForm.Path, ContactForm.Valid());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await app.MessagesAsync());
        Assert.Empty(app.Outbox.Messages);
    }

    [Fact]
    public async Task FilledHoneypotIsAnsweredAsASuccessAndDropped()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With("website", "https://spam.example"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await App.MessagesAsync());
        Assert.Empty(App.Outbox.Messages);
    }

    [Fact]
    public async Task FilledHoneypotIsDroppedEvenWhenTheRestIsInvalid()
    {
        using var browser = App.Browser();
        var body = ContactForm.With("website", "x");
        body["email"] = "pas-une-adresse";

        using var response = await browser.PostAsync(ContactForm.Path, body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await App.MessagesAsync());
    }

    [Fact]
    public async Task ForeignOriginIsRefused()
    {
        using var browser = App.Browser();
        using var request = browser.Post(ContactForm.Path, ContactForm.Valid());
        request.Headers.Remove(BrowserClient.OriginHeader);
        request.Headers.Add(BrowserClient.OriginHeader, "https://evil.example");

        using var response = await browser.SendAsync(request);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Empty(await App.MessagesAsync());
        Assert.Empty(App.Outbox.Messages);
    }

    [Fact]
    public async Task SignedInSenderIsRecordedWithItsMessage()
    {
        await using var accounts = await AccountsApp.StartAsync(postgres, withGoogle: false);
        var user = await accounts.SeedAsync(new AccountSeed());
        using var browser = accounts.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);

        using var response = await browser.PostAsync(ContactForm.Path, ContactForm.Valid());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            [user.Id.ToString(CultureInfo.InvariantCulture)],
            await accounts.QueryAsync("SELECT user_id FROM contact_messages"));
    }

    public async ValueTask InitializeAsync() => _app = await ContactApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
