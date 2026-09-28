using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Outbox.Smtp;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// The outbox zone as the API registers it, on the test's database, with a fixed clock, the
/// logs kept and, unless the test wants the real relay, a recording transport.
/// </summary>
internal sealed class OutboxHarness : IAsyncDisposable
{
    public const string Recipient = "player@example.com";
    public const string ActionUrl = "https://lodb.test/account/link?token=a&sig=b";

    public static readonly DateTimeOffset Start = new(2026, 9, 26, 8, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    /// <param name="connectionString">The test's database.</param>
    /// <param name="settings">Settings over the defaults (a relay at <c>mail.test</c>).</param>
    /// <param name="transport">The transport; the MailKit one when null.</param>
    public OutboxHarness(
        string connectionString,
        IReadOnlyDictionary<string, string?>? settings = null,
        IMailTransport? transport = null)
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{LoDbDataSource.ConnectionStringName}"] = connectionString,
            ["LoDb:Mail:Host"] = "mail.test",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<TimeProvider>(Time)
            .AddFakeLogging()
            .AddMetrics();
        if (transport is not null)
        {
            services.AddSingleton(transport);
        }

        _services = services
            .AddLoDbPersistence(configuration)
            .AddLoDbOutbox(configuration)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
    }

    public FakeTimeProvider Time { get; } = new(Start);

    public IOutboxDispatcher Dispatcher => _services.GetRequiredService<IOutboxDispatcher>();

    public IServiceProvider Services => _services;

    /// <summary>Every log line of the zone, messages and structured values.</summary>
    public IReadOnlyList<FakeLogRecord> Logs => _services.GetFakeLogCollector().GetSnapshot();

    public static EmailMessage Confirmation(UiLocale locale, string recipient = Recipient) => new()
    {
        Recipient = recipient,
        Template = EmailTemplate.ConfirmEmail,
        Locale = locale,
        Model = AccountModel(),
    };

    public static EmailMessage Contact(string recipient, string visitor) => new()
    {
        Recipient = recipient,
        Template = EmailTemplate.ContactNotification,
        Locale = UiLocale.Fr,
        Model = new Dictionary<string, string?>(ContactModel())
        {
            [EmailModelKeys.ContactEmail] = visitor,
        },
    };

    /// <summary>The model of a confirmation or a reset.</summary>
    public static Dictionary<string, string?> AccountModel(
        string? minutes = null,
        string userName = "Faker") =>
        new()
        {
            [EmailModelKeys.ActionUrl] = ActionUrl,
            [EmailModelKeys.UserName] = userName,
            [EmailModelKeys.ExpiresInMinutes] = minutes,
        };

    /// <summary>The model of a contact notification, as the contact module fills it.</summary>
    public static Dictionary<string, string?> ContactModel(
        string? subject = "Page blanche",
        string? name = "Visiteur") =>
        new()
        {
            [EmailModelKeys.ContactCategory] = "bug",
            [EmailModelKeys.ContactName] = name,
            [EmailModelKeys.ContactEmail] = "visitor@example.com",
            [EmailModelKeys.ContactSubject] = subject,
            [EmailModelKeys.ContactMessage] = "La page des runes reste blanche.\nMerci !",
            [EmailModelKeys.ContactReceivedAt] = "2026-09-26T10:15:00+02:00",
            [EmailModelKeys.ContactLocale] = "fr",
        };

    /// <summary>Queues a message and saves it, as a caller of the outbox does.</summary>
    public async Task EnqueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();
        await outbox.EnqueueAsync(message, cancellationToken);
        await scope.ServiceProvider.GetRequiredService<LoDbDbContext>()
            .SaveChangesAsync(cancellationToken);
    }

    /// <summary>The rows of <c>email_outbox</c>, oldest first.</summary>
    public async Task<IReadOnlyList<EmailOutboxMessage>> RowsAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        return await context.EmailOutbox.AsNoTracking()
            .OrderBy(static message => message.Id)
            .ToListAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
