using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Contact.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Contact;

/// <summary>
/// The rules of the legacy form, every broken one reported at once, and nothing kept or
/// sent for a refused message.
/// </summary>
public sealed class ContactValidationTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
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

    [Theory]
    [InlineData("email", "pas-une-adresse", "invalid")]
    [InlineData("email", "visiteur@localhost", "invalid")]
    [InlineData("message", "  Neuf car.  ", "too-short")]
    public async Task BrokenFieldIsNamed(string field, string value, string code)
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With(field, value));

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal([code], Assert.Single(errors, pair => pair.Key == field).Value);
        Assert.Single(errors);
    }

    [Theory]
    [InlineData("name", 120)]
    [InlineData("subject", 160)]
    [InlineData("message", 5000)]
    [InlineData("email", 255)]
    public async Task FieldLongerThanItsColumnIsRefused(string field, int max)
    {
        using var browser = App.Browser();
        var longest = field == "email" ? Address(max) : new string('a', max);
        var tooLong = field == "email" ? Address(max + 1) : new string('a', max + 1);

        using var accepted = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With(field, longest));
        using var refused = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With(field, tooLong));

        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var errors = await ApiJson.FieldErrorsAsync(refused);
        Assert.Equal(["too-long"], errors[field]);
        Assert.Single(await App.MessagesAsync());
    }

    [Fact]
    public async Task LengthsCountCharactersNotCodeUnits()
    {
        using var browser = App.Browser();
        // Ten emojis: twenty UTF-16 units, ten characters as PHP counted them.
        var emojis = string.Concat(Enumerable.Repeat("\U0001F980", 10));

        using var accepted = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With("message", emojis));
        using var refused = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With("message", emojis[..^2]));

        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal(["too-short"], (await ApiJson.FieldErrorsAsync(refused))["message"]);
    }

    [Fact]
    public async Task UnknownCategoryIsRefused()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(
            ContactForm.Path,
            ContactForm.With("category", "sales"));

        Assert.Equal(["invalid"], (await ApiJson.FieldErrorsAsync(response))["category"]);
        Assert.Empty(await App.MessagesAsync());
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
