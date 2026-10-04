using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi;

/// <summary>
/// Refuses at startup a site origin that is not a bare http(s) origin, which every
/// <c>share_url</c> would carry, and durations that are not positive.
/// </summary>
internal sealed class PublicApiOptionsValidator : IValidateOptions<PublicApiOptions>
{
    public ValidateOptionsResult Validate(string? name, PublicApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!WebOrigin.TryParse(options.SiteOrigin, out _))
        {
            failures.Add(
                $"SiteOrigin must read as https://host[:port], not '{options.SiteOrigin}'.");
        }

        RequirePositive(failures, nameof(options.KeyCacheLifetime), options.KeyCacheLifetime);
        RequirePositive(failures, nameof(options.TrendsLifetime), options.TrendsLifetime);
        RequirePositive(failures, nameof(options.MeteringInterval), options.MeteringInterval);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(List<string> failures, string name, TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{name} must be positive, not {value}.");
        }
    }
}
