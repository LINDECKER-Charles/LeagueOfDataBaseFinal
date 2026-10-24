using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts;

/// <summary>
/// Refuses at startup a site origin that is not one, a Google client without its secret
/// (Google would refuse every sign-in, long after the deployment), and the waiver of the
/// second factor outside Development.
/// </summary>
internal sealed class AccountsOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<AccountsOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = CheckSiteOrigin(options.SiteOrigin)
            .Concat(CheckGoogle(options.Google))
            .Concat(CheckWaiver(options.WaiveSecondFactor))
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

    private IEnumerable<string> CheckWaiver(bool isWaived)
    {
        if (isWaived && !environment.IsDevelopment())
        {
            yield return "WaiveSecondFactor is for Development, not "
                + $"{environment.EnvironmentName}.";
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
