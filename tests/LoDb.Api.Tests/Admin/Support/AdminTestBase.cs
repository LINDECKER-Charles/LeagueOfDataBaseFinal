using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.Support;

/// <summary>An accounts host on a database of its own for each test of the admin API.</summary>
public abstract class AdminTestBase(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private AccountsApp? _app;

    protected AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>The body of a <c>GET</c> answered with 200.</summary>
    protected static async Task<JsonElement> ReadAsync(BrowserClient browser, string path)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var response = await browser.GetAsync(path);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    /// <summary>The last journal entry, which must record <paramref name="action"/>.</summary>
    protected async Task<AuditLogEntry> LastAuditAsync(AuditAction action)
    {
        var entry = (await App.AuditAsync())[^1];
        Assert.Equal(action, entry.Action);
        return entry;
    }

    /// <summary>A field of the <c>meta</c> of a journal entry, whatever its JSON kind.</summary>
    protected static string? RawMeta(AuditLogEntry entry, string key)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var meta = JsonDocument.Parse(entry.Meta ?? "{}");
        return meta.RootElement.TryGetProperty(key, out var value) ? value.ToString() : null;
    }
}
