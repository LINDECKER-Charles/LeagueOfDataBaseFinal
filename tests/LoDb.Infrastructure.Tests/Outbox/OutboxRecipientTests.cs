using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Tests.Outbox.Support;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// A recipient that is not one address is refused as the message is queued, before any
/// database work, and the refusal does not repeat it: exceptions end up in the logs.
/// </summary>
public sealed class OutboxRecipientTests
{
    // Blank, then unreadable as one address (a list is the case that matters).
    [Theory]
    [InlineData("")]
    [InlineData("a@example.com, b@example.com")]
    public async Task InvalidRecipientIsRefusedWithoutEchoingIt(string recipient)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var harness = new OutboxHarness(ApiFactory.UnreachablePostgres);
        await using var scope = harness.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IEmailOutbox>();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            outbox.EnqueueAsync(OutboxHarness.Confirmation(UiLocale.En, recipient), cancellation));

        Assert.True(
            recipient.Length == 0
            || !error.Message.Contains(recipient, StringComparison.Ordinal));
    }
}
