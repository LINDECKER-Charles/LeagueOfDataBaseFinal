using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.Support;

/// <summary>
/// An accounts host for the tests of the admin API: the class's, shared by its tests, or one
/// of each test's own when what the host holds is the subject (counters over a whole table,
/// caches, the meters, the console).
/// </summary>
public abstract class AdminTestBase : IAsyncLifetime
{
    private readonly PostgresContainerFixture? _postgres;
    private AccountsApp? _app;

    /// <summary>A host started for each test, on a database of its own.</summary>
    protected AdminTestBase(PostgresContainerFixture postgres) =>
        _postgres = postgres ?? throw new ArgumentNullException(nameof(postgres));

    /// <summary>The host of the class: each test names its accounts with it.</summary>
    protected AdminTestBase(SharedAccountsApp host) =>
        _app = (host ?? throw new ArgumentNullException(nameof(host))).App;

    protected AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (_postgres is not null)
        {
            _app = await AccountsApp.StartAsync(_postgres);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // The shared host is its fixture's to dispose.
        if (_postgres is not null && _app is not null)
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

    /// <summary>
    /// The last journal entry, which must record <paramref name="action"/>: the test's own, as
    /// the tests of a class run one after the other.
    /// </summary>
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
