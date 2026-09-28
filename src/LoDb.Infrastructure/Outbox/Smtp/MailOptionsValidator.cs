using Microsoft.Extensions.Options;
using MimeKit;

namespace LoDb.Infrastructure.Outbox.Smtp;

/// <summary>Refuses at startup a relay setting that could never send.</summary>
internal sealed class MailOptionsValidator : IValidateOptions<MailOptions>
{
    private const int MaxPort = 65535;
    private static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(5);

    public ValidateOptionsResult Validate(string? name, MailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.Port is < 1 or > MaxPort)
        {
            failures.Add($"Port must lie between 1 and {MaxPort}.");
        }

        if (!MailboxAddress.TryParse(options.From, out _))
        {
            failures.Add("From must be one address, with an optional display name.");
        }

        if (options.Timeout < MinTimeout || options.Timeout > MaxTimeout)
        {
            failures.Add("Timeout must lie between one second and five minutes.");
        }

        if (!Enum.IsDefined(options.Security))
        {
            failures.Add("Security must name a TLS mode.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
