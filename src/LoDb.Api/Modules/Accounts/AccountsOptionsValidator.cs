using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts;

/// <summary>
/// Refuses at startup a site origin that is not one, and a Google client without its
/// secret: Google would refuse every sign-in, long after the deployment.
/// </summary>
internal sealed class AccountsOptionsValidator : IValidateOptions<AccountsOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = CheckSiteOrigin(options.SiteOrigin)
            .Concat(CheckGoogle(options.Google))
            .ToList();
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> CheckSiteOrigin(string? origin)
    {
        if (origin is not null && !WebOrigin.TryParse(origin, out _))
        {
            yield return $"SiteOrigin must read as https://example.com, not '{origin}'.";
        }
    }

    private static IEnumerable<string> CheckGoogle(GoogleAccountOptions google)
    {
        if (google.IsEnabled && string.IsNullOrEmpty(google.ClientSecret))
        {
            yield return "Google:ClientSecret is required with Google:ClientId.";
        }

        // The exchange goes through the web client's handler and its backchannel.
        if (!google.IsEnabled && google.AppClients.Count > 0)
        {
            yield return "Google:AppClients need the web client, Google:ClientId.";
        }

        for (var index = 0; index < google.AppClients.Count; index++)
        {
            if (string.IsNullOrEmpty(google.AppClients[index].ClientId))
            {
                yield return $"Google:AppClients:{index}:ClientId is required.";
            }
        }
    }
}
