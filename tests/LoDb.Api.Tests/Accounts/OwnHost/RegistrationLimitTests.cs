using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>One address registers five accounts an hour at most.</summary>
/// <remarks>A host of its own: the test reaches the limit, which lives in the host.</remarks>
public sealed class RegistrationLimitTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string RegisterPath = "/api/account/register";
    private const int RegistrationsPerHour = 5;

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task OneAddressRegistersFiveAccountsAnHour()
    {
        using var browser = App.Browser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= RegistrationsPerHour; attempt++)
        {
            using var response = await RegisterFromAsync(browser, "203.0.113.7");
            statuses.Add(response.StatusCode);
        }

        using var limited = await RegisterFromAsync(browser, "203.0.113.7");
        using var other = await RegisterFromAsync(browser, "203.0.113.8");

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.BadRequest, RegistrationsPerHour),
                HttpStatusCode.TooManyRequests],
            statuses);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(limited, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    // Refused by its rules, which does not spare the limit: each attempt counts.
    private static async Task<HttpResponseMessage> RegisterFromAsync(
        BrowserClient browser,
        string address)
    {
        using var request = browser.Post(RegisterPath, new
        {
            email = "not-an-email",
            username = "Nouveau_7",
            password = AccountSeed.StrongPassword,
            acceptTerms = true,
        });
        request.Headers.Add(BrowserClient.ForwardedForHeader, address);
        return await browser.SendAsync(request);
    }
}
