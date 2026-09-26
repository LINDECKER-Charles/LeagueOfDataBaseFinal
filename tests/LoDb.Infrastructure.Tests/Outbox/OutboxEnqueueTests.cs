using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Contact;
using LoDb.Infrastructure.Persistence.Outbox;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// A message is written with the change that queues it, in the same transaction, or not at
/// all.
/// </summary>
public sealed class OutboxEnqueueTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private OutboxHarness? _harness;

    private OutboxHarness Harness => _harness ??= new OutboxHarness(Database.ConnectionString);

    [Fact]
    public async Task MessageIsWrittenPendingAndDueAtOnce()
    {
        await Harness.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.Fr), Cancellation);

        var row = Assert.Single(await Harness.RowsAsync(Cancellation));
        Assert.Equal(OutboxHarness.Recipient, row.Recipient);
        Assert.Equal(EmailTemplate.ConfirmEmail, row.Template);
        Assert.Equal(UiLocale.Fr, row.Locale);
        Assert.Equal(EmailOutboxStatus.Pending, row.Status);
        Assert.Equal(0, row.Attempts);
        Assert.Equal(OutboxHarness.Start, row.NextAttemptAt);
        Assert.Equal(OutboxHarness.Start, row.CreatedAt);
        Assert.Null(row.SentAt);
        var model = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Model)!;
        Assert.Equal(OutboxHarness.ActionUrl, model[EmailModelKeys.ActionUrl]);
        Assert.Equal(
            ["confirm_email|fr|pending"],
            await Database.QueryAsync(
                "SELECT template || '|' || locale || '|' || status FROM email_outbox",
                Cancellation));
    }

    [Fact]
    public async Task NothingIsWrittenWithoutTheCallersSave()
    {
        await using (var scope = Harness.Services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();
            await outbox.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);
        }

        Assert.Empty(await Harness.RowsAsync(Cancellation));
    }

    [Fact]
    public async Task RolledBackChangeTakesItsMessageAlong()
    {
        await using (var scope = Harness.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
            await using var transaction =
                await context.Database.BeginTransactionAsync(Cancellation);
            context.ContactMessages.Add(Contact(userId: null));
            await scope.ServiceProvider.GetRequiredService<IEmailOutbox>()
                .EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);
            await context.SaveChangesAsync(Cancellation);
            await transaction.RollbackAsync(Cancellation);
        }

        Assert.Empty(await Harness.RowsAsync(Cancellation));
        Assert.Equal(["0"], await CountContactsAsync());
    }

    [Fact]
    public async Task FailedChangeWritesNoMessage()
    {
        await using var scope = Harness.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();

        // No such account: the foreign key refuses the change, and the message with it.
        context.ContactMessages.Add(Contact(userId: 404));
        await scope.ServiceProvider.GetRequiredService<IEmailOutbox>()
            .EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En), Cancellation);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Cancellation));
        Assert.Empty(await Harness.RowsAsync(Cancellation));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an address")]
    [InlineData("a@example.com, b@example.com")]
    public async Task InvalidRecipientIsRefusedWithoutEchoingIt(string recipient)
    {
        await using var scope = Harness.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            outbox.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En, recipient), Cancellation));

        Assert.True(
            recipient.Length == 0
            || !error.Message.Contains(recipient, StringComparison.Ordinal));
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    private static ContactMessage Contact(int? userId) => new()
    {
        Category = "bug",
        Email = "visitor@example.com",
        Message = "Hello",
        Status = "new",
        CreatedAt = OutboxHarness.Start,
        UserId = userId,
    };

    private Task<IReadOnlyList<string>> CountContactsAsync() =>
        Database.QueryAsync("SELECT count(*) FROM contact_messages", Cancellation);
}
