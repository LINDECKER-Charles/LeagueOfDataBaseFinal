using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Contact;
using LoDb.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Contact.Support;

/// <summary>
/// The API over a database of its own, its outbox recording what it is given, with or
/// without a mailbox to forward the messages to.
/// </summary>
/// <remarks>One per test: the rate limits live in the host.</remarks>
public sealed class ContactApp : IAsyncDisposable
{
    public const string Recipient = "equipe@example.test";

    private const string RecipientKey = "LoDb:Contact:Recipient";
    private const string Accounts = "LoDb:Accounts:";

    private readonly TestDatabase _database;
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _host;

    private ContactApp(TestDatabase database, string? recipient)
    {
        _database = database;
        _factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Clock = Clock,
            Settings = new Dictionary<string, string?>
            {
                [Accounts + "SecureCookies"] = bool.TrueString,
                [Accounts + "SiteOrigin"] = AccountsApp.SiteOrigin,
                [RecipientKey] = recipient,
            },
        };
        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IEmailOutbox>(Outbox)));
    }

    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    public RecordingOutbox Outbox { get; } = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <param name="postgres">The server the database of the host is created on.</param>
    /// <param name="recipient">The mailbox of the team; none keeps the messages only.</param>
    public static async Task<ContactApp> StartAsync(
        PostgresContainerFixture postgres,
        string? recipient = Recipient)
    {
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        return new ContactApp(database, recipient);
    }

    /// <summary>A browser on the site, as the form of the footer posts.</summary>
    public BrowserClient Browser() => BrowserClient.Open(_host);

    /// <summary>The stored messages, oldest first.</summary>
    public async Task<IReadOnlyList<ContactMessage>> MessagesAsync()
    {
        await using var context = _database.CreateContext();
        return await context.ContactMessages.AsNoTracking()
            .OrderBy(static message => message.Id)
            .ToListAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        // Disposes the host derived from it as well.
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
