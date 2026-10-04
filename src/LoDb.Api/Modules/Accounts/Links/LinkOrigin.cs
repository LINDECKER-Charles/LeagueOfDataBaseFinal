using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>The origin of the links sent by e-mail.</summary>
/// <remarks>
/// The configured site origin, never the <c>Host</c> of the request, which nginx does not
/// check: a forged one would send the reset link of a victim to the forger's site. Only a
/// developer's machine falls back to the request.
/// </remarks>
internal sealed class LinkOrigin(
    IOptions<AccountsOptions> options,
    IHostEnvironment environment,
    IHttpContextAccessor accessor)
{
    /// <summary>The origin, such as <c>https://leagueofdatabase.com</c>; null if unknown.</summary>
    public string? Resolve()
    {
        if (WebOrigin.TryParse(options.Value.SiteOrigin, out var configured))
        {
            return WebOrigin.Format(configured);
        }

        return environment.IsDevelopment() && accessor.HttpContext is { } context
            ? WebOrigin.Of(context.Request)
            : null;
    }
}
