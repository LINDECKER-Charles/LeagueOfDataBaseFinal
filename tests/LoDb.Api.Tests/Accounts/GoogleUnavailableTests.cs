using System.Net;
using System.Net.Http.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// Without a Google client configured, Google sign-in is unavailable on the web and to the
/// apps, rather than failing at Google.
/// </summary>
public sealed class GoogleUnavailableTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task WithoutAClientIdGoogleIsUnavailableToTheWebAndToTheApps()
    {
        await using var withoutGoogle = await AccountsApp.StartAsync(postgres, withGoogle: false);
        using var browser = withoutGoogle.Browser();
        using var app = withoutGoogle.App();

        using var start = await browser.GetAsync("/api/account/google/start?locale=it");
        using var exchange = await app.PostAsJsonAsync(
            new Uri("/api/account/google/app/exchange", UriKind.Relative),
            new
            {
                code = FakeGoogle.Code,
                codeVerifier = new string('v', 43),
                redirectUri = "com.leagueofdatabase.app:/oauth2redirect",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, start.StatusCode);
        Assert.Equal(
            "/it/account/login?error=google-unavailable",
            start.Headers.Location?.OriginalString);
        Assert.Equal(
            "google-unavailable",
            await ApiJson.ProblemCodeAsync(exchange, HttpStatusCode.ServiceUnavailable));
    }
}
