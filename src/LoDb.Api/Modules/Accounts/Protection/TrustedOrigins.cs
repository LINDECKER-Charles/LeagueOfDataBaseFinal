using System.Collections.Frozen;
using LoDb.Api.Modules.Accounts.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// The origins whose pages may send unsafe requests: the site itself, its configured public
/// origin, and the Android app.
/// </summary>
internal sealed class TrustedOrigins
{
    // The Android app serves its bundle from this origin (Capacitor, ADR 0007).
    private const string AndroidApp = "https://localhost";

    private readonly FrozenSet<string> _configured;

    public TrustedOrigins(IOptions<AccountsOptions> options)
    {
        string[] site = WebOrigin.TryParse(options.Value.SiteOrigin, out var origin)
            ? [WebOrigin.Format(origin)]
            : [];
        _configured = site.Append(AndroidApp).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the <c>Origin</c> of the request is trusted. A request without one is not a
    /// browser's cross-site request, and one of <c>null</c> is refused.
    /// </summary>
    public bool Allows(HttpRequest request)
    {
        var header = request.Headers.Origin;
        if (header.Count == 0)
        {
            return true;
        }

        return header.Count == 1
            && WebOrigin.TryParse(header[0], out var origin)
            && (IsOwn(origin, request) || _configured.Contains(WebOrigin.Format(origin)));
    }

    // nginx forwards the host without its port: the port only counts when the Host carries
    // one. A page on another port of the same host shares the cookies anyway.
    private static bool IsOwn(Uri origin, HttpRequest request) =>
        string.Equals(origin.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(origin.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
        && (request.Host.Port is not { } port || port == origin.Port);
}
