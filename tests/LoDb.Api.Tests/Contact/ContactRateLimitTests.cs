using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Contact.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Contact;

/// <summary>
/// The <c>contact</c> rate limit: one address sends five messages an hour, refused ones
/// included, and the others keep theirs.
/// </summary>
public sealed class ContactRateLimitTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string ForwardedFor = "X-Forwarded-For";
    private const int MessagesPerHour = 5;

    private ContactApp? _app;

    private ContactApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task OneAddressSendsFiveMessagesAnHour()
    {
        using var browser = App.Browser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < MessagesPerHour; attempt++)
        {
            using var response = await SendFromAsync(browser, "203.0.113.7", attempt == 0);
            statuses.Add(response.StatusCode);
        }

        using var limited = await SendFromAsync(browser, "203.0.113.7", valid: true);
        using var other = await SendFromAsync(browser, "203.0.113.8", valid: true);

        Assert.Equal(
            [HttpStatusCode.NoContent,
                .. Enumerable.Repeat(HttpStatusCode.BadRequest, MessagesPerHour - 1)],
            statuses);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(limited, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
        Assert.Equal(2, (await App.MessagesAsync()).Count);
        Assert.Equal(2, App.Outbox.Messages.Count);
    }

    public async ValueTask InitializeAsync() => _app = await ContactApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<HttpResponseMessage> SendFromAsync(
        BrowserClient browser,
        string address,
        bool valid)
    {
        var body = valid ? ContactForm.Valid() : ContactForm.With("email", "pas-une-adresse");
        using var request = browser.Post(ContactForm.Path, body);
        request.Headers.Add(ForwardedFor, address);
        return await browser.SendAsync(request);
    }
}
