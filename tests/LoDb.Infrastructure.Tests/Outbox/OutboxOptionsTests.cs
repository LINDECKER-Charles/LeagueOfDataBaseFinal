using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Outbox.Delivery;
using LoDb.Infrastructure.Outbox.Smtp;
using MailKit.Security;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// The defaults are valid and send nothing until a relay is set; a setting that could never
/// work is refused at startup.
/// </summary>
public sealed class OutboxOptionsTests
{
    [Fact]
    public void DefaultsAreValidAndLeaveSendingOff()
    {
        var mail = new MailOptions();

        Assert.True(new MailOptionsValidator().Validate(null, mail).Succeeded);
        Assert.True(new OutboxOptionsValidator().Validate(null, new OutboxOptions()).Succeeded);
        Assert.False(mail.IsEnabled);
        Assert.Equal(MailOptions.DefaultFrom, mail.From);
    }

    [Theory]
    [InlineData(0, "no-reply@example.com", 30)]
    [InlineData(70000, "no-reply@example.com", 30)]
    [InlineData(1025, "not an address", 30)]
    [InlineData(1025, "no-reply@example.com", 0)]
    public void UnusableRelayIsRefused(int port, string from, int timeoutSeconds)
    {
        var options = new MailOptions
        {
            Host = "mailpit",
            Port = port,
            From = from,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            Security = SecureSocketOptions.None,
        };

        Assert.True(new MailOptionsValidator().Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(nameof(OutboxOptions.BatchSize))]
    [InlineData(nameof(OutboxOptions.MaxAttempts))]
    [InlineData(nameof(OutboxOptions.PollInterval))]
    [InlineData(nameof(OutboxOptions.RetryDelay))]
    [InlineData(nameof(OutboxOptions.MaxRetryDelay))]
    [InlineData(nameof(OutboxOptions.Lease))]
    public void StallingPaceIsRefused(string setting)
    {
        var options = Broken(setting);

        Assert.True(new OutboxOptionsValidator().Validate(null, options).Failed);
    }

    private static OutboxOptions Broken(string setting) => setting switch
    {
        nameof(OutboxOptions.BatchSize) => new OutboxOptions { BatchSize = 0 },
        nameof(OutboxOptions.MaxAttempts) => new OutboxOptions { MaxAttempts = 0 },
        nameof(OutboxOptions.PollInterval) => new OutboxOptions { PollInterval = TimeSpan.Zero },
        nameof(OutboxOptions.RetryDelay) => new OutboxOptions { RetryDelay = TimeSpan.Zero },
        // A cap below the first wait.
        nameof(OutboxOptions.MaxRetryDelay) =>
            new OutboxOptions { MaxRetryDelay = TimeSpan.FromSeconds(1) },
        nameof(OutboxOptions.Lease) => new OutboxOptions { Lease = TimeSpan.FromDays(2) },
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, null),
    };
}
