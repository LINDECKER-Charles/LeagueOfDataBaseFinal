using System.Net;
using LoDb.Api.Modules.Contact;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Contact.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Contact;

/// <summary>
/// The rules of the legacy form through the endpoint: every broken one reported at once,
/// nothing kept or sent for a refused message, and the longest fields stored whole.
/// </summary>
public sealed class ContactValidationTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private ContactApp? _app;

    private ContactApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task EveryMissingFieldIsReportedTogether()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(
            ContactForm.Path,
            new { message = "   ", email = " " });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["category", "email", "message"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.All(errors.Values, static codes => Assert.Equal(["required"], codes));
        Assert.Empty(await App.MessagesAsync());
        Assert.Empty(App.Outbox.Messages);
    }

    // The longest fields the rules accept fit their columns; the rules are ContactRulesTests'.
    [Fact]
    public async Task FieldsAtTheirLongestAreStored()
    {
        using var browser = App.Browser();
        var body = ContactForm.Valid();
        body["name"] = new string('a', ContactRules.MaxNameLength);
        body["email"] = Address(ContactRules.MaxEmailLength);
        body["subject"] = new string('a', ContactRules.MaxSubjectLength);
        body["message"] = new string('a', ContactRules.MaxMessageLength);

        using var response = await browser.PostAsync(ContactForm.Path, body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = Assert.Single(await App.MessagesAsync());
        Assert.Equal(
            (ContactRules.MaxNameLength, ContactRules.MaxEmailLength,
                ContactRules.MaxSubjectLength, ContactRules.MaxMessageLength),
            (stored.Name?.Length, stored.Email.Length, stored.Subject?.Length,
                stored.Message.Length));
    }

    public async ValueTask InitializeAsync() => _app = await ContactApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    // A valid address of exactly `length` characters.
    private static string Address(int length)
    {
        const string domain = "@example.test";
        return new string('v', length - domain.Length) + domain;
    }
}
