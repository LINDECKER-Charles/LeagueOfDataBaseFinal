using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>
/// Refuses at startup a pass that would render for something other than a bare host, ask the
/// SSR through anything but a base URL, write epoch 0, or keep no pace or no bound on its pages
/// in flight, or whose adaptive pace does not hold together (<see cref="PaceOptionsRules"/>).
/// Missing settings are not refused: the pass then stays dormant.
/// </summary>
/// <remarks>
/// The SSR is reached over plain http on purpose: the stack network carries it, and the SSR
/// only answers the pass from there. The store's endpoint, which leaves the host, is held to
/// https by the page store's own validator.
/// </remarks>
internal sealed class PrerenderOptionsValidator : IValidateOptions<PrerenderOptions>
{
    private const string RootPath = "/";

    // Pages in flight are rendered, then uploaded: at the manual maximum of a few renderers,
    // some 20 of them overlap. Twice that is a bound on a typo.
    private const int MaxPagesInFlight = 40;

    // Each renderer is a core of the host at most: past the VPS's own cores, a typo.
    private const int MaxRenderers = 8;

    public ValidateOptionsResult Validate(string? name, PrerenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddIf(
            failures,
            !string.IsNullOrWhiteSpace(options.PublicHost) && !IsHost(options.PublicHost),
            "PublicHost must be a host name, with a port at most: no scheme, path nor query.");
        AddIf(
            failures,
            !string.IsNullOrWhiteSpace(options.SsrBaseUrl) && !IsBaseUrl(options.SsrBaseUrl),
            "SsrBaseUrl must be an http or https base URL: a scheme and a host, no path.");
        AddIf(
            failures,
            options.ServedEpoch < PrerenderOptions.FirstEpoch,
            "ServedEpoch must be 1 or more: epoch 0 is reserved for the store probes.");
        AddIf(
            failures,
            options.RendersPerMinute is < 1 or > PrerenderOptions.MostRendersPerMinute,
            $"RendersPerMinute must be between 1 and {PrerenderOptions.MostRendersPerMinute}.");
        AddIf(
            failures,
            options.MaxInFlight is < 1 or > MaxPagesInFlight,
            $"MaxInFlight must be between 1 and {MaxPagesInFlight}.");
        AddIf(
            failures,
            options.Renderers is < 1 or > MaxRenderers,
            $"Renderers must be between 1 and {MaxRenderers}.");
        failures.AddRange(PaceOptionsRules.FailuresOf(options));
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    // The host and port alone, as an authority of its own: anything else would change how the
    // URL parses.
    private static bool IsHost(string host) =>
        Uri.TryCreate($"{Uri.UriSchemeHttps}://{host}{RootPath}", UriKind.Absolute, out var url)
        && string.Equals(url.Authority, host, StringComparison.OrdinalIgnoreCase)
        && url.AbsolutePath == RootPath
        && url.UserInfo.Length == 0;

    private static bool IsBaseUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
        && url.AbsolutePath == RootPath
        && url.Query.Length == 0
        && url.Fragment.Length == 0
        && url.UserInfo.Length == 0;

    private static void AddIf(List<string> failures, bool failed, string message)
    {
        if (failed)
        {
            failures.Add(message);
        }
    }
}
