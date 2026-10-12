using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.PublicApiKeys.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.PublicApiKeys.OwnHost;

/// <summary>Keys are issued to one account five times an hour at most.</summary>
/// <remarks>A host of its own: the test reaches the limit, which lives in the host.</remarks>
public sealed class KeyIssueLimitTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const int IssuesPerHour = 5;

    private KeysApp? _app;

    private KeysApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task KeysIssuedToOneAccountAreLimitedToFiveAnHour()
    {
        var owner = await App.SeedUserAsync("Pressee");
        using var browser = await App.SignedInAsync(owner);
        using var other = await App.SignedInAsync(await App.SeedUserAsync("Posee"));
        var statuses = new List<HttpStatusCode>();

        // Creations and regenerations share the limit.
        using var created = await browser.PostAsync(KeysApp.PortalPath, new { name = "app" });
        statuses.Add(created.StatusCode);
        for (var attempt = 1; attempt < IssuesPerHour; attempt++)
        {
            using var regenerated = await browser.PostAsync(KeysApp.RegeneratePath);
            statuses.Add(regenerated.StatusCode);
        }

        using var refused = await browser.PostAsync(KeysApp.RegeneratePath);
        using var otherAccount = await other.PostAsync(KeysApp.PortalPath, new { name = "app" });

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.OK, otherAccount.StatusCode);
        Assert.Equal(IssuesPerHour, (await App.KeysOfAsync(owner.Id)).Count);
    }

    public async ValueTask InitializeAsync() => _app = await KeysApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
