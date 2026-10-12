using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Backups;

/// <summary>Refuses at startup a price the panel could not show.</summary>
internal sealed partial class BackupOptionsValidator : IValidateOptions<BackupOptions>
{
    public ValidateOptionsResult Validate(string? name, BackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.PricePerGbMonth < 0)
        {
            failures.Add("PricePerGbMonth must not be negative.");
        }

        if (!CurrencyCode().IsMatch(options.Currency))
        {
            failures.Add("Currency must be an ISO 4217 code, three capital letters.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyCode();
}
