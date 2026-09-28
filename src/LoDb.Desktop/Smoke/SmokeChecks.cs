using LoDb.Desktop.Hosting;
using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Proxy;

namespace LoDb.Desktop.Smoke;

/// <summary>
/// The checks of <c>--smoke</c>, run against the started host through its own loopback
/// port. None reaches the network unless <c>--probe-api</c> asks for it.
/// </summary>
internal sealed class SmokeChecks(LoopbackHost host, HttpClient loopback)
{
    private const string MarkerName = "__LODB_DESKTOP__";
    private const string ProbePath = DesktopRoutes.Api + "/meta";
    private const string Ok = "ok";
    private const string Failed = "failed";

    public async Task<SmokeCheck> CheckHostAsync(CancellationToken cancellationToken)
    {
        var isServing = await IsSuccessAsync(DesktopRoutes.Health, cancellationToken);
        return Check("host", isServing, isServing ? Ok : Failed, host.Address.ToString());
    }

    public SmokeCheck CheckProxy()
    {
        var origin = host.Services.GetRequiredService<DesktopOptions>().ApiOrigin;
        var isMapped = host.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Any(endpoint => endpoint.RoutePattern.RawText == ApiProxy.Pattern);
        var detail = $"{DesktopRoutes.Api}/** -> {origin.GetLeftPart(UriPartial.Authority)}";
        return Check("proxy", isMapped, isMapped ? "ready" : "missing", detail);
    }

    public async Task<SmokeCheck> CheckShellAsync(CancellationToken cancellationToken)
    {
        var index = host.Services.GetRequiredService<ShellIndex>();
        if (!index.IsAvailable)
        {
            return Check("shell", false, "missing", index.FilePath);
        }

        var page = await loopback.GetStringAsync(new Uri("/", UriKind.Relative), cancellationToken);
        var hasMarker = page.Contains(MarkerName, StringComparison.Ordinal);
        return Check("shell", hasMarker, hasMarker ? Ok : "no-marker", index.FilePath);
    }

    public SmokeCheck CheckUpdates()
    {
        var current = host.Services.GetRequiredService<IDesktopUpdates>().Current;
        var stage = current.Stage.ToString().ToLowerInvariant();
        return Check("updates", true, stage, current.Version ?? string.Empty);
    }

    public async Task<SmokeCheck> ProbeApiAsync(
        bool shouldProbe,
        CancellationToken cancellationToken)
    {
        if (!shouldProbe)
        {
            return Check("api", true, "skipped", "(--probe-api to call it)");
        }

        using var response = await loopback.GetAsync(
            new Uri(ProbePath, UriKind.Relative),
            cancellationToken);
        var isReachable = response.IsSuccessStatusCode;
        var detail = $"HTTP {(int)response.StatusCode} {ProbePath}";
        return Check("api", isReachable, isReachable ? "reachable" : "unreachable", detail);
    }

    private async Task<bool> IsSuccessAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await loopback.GetAsync(
            new Uri(path, UriKind.Relative),
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static SmokeCheck Check(string name, bool isHealthy, string state, string detail) =>
        new() { Name = name, IsHealthy = isHealthy, State = state, Detail = detail };
}
