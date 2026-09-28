using LoDb.Desktop.Hosting;
using LoDb.Desktop.Launch;

namespace LoDb.Desktop.Smoke;

/// <summary>
/// <c>--smoke</c>: starts the host without a window, prints the version and the state of
/// the proxy, then exits (0 healthy, 1 otherwise). The update E2E of L9.4 reads the first
/// line to see which version runs.
/// </summary>
internal static class SmokeRun
{
    private const int Healthy = 0;
    private const int Unhealthy = 1;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static async Task<int> RunAsync(
        DesktopLaunch launch,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var options = launch.Options;
        await output.WriteLineAsync($"LoDb.Desktop {options.Version} (channel {options.Channel})");
        await using var host = await LoopbackHost.StartAsync(
            options,
            overrides: null,
            cancellationToken);
        using var loopback = new HttpClient
        {
            BaseAddress = host.Address,
            Timeout = RequestTimeout,
        };
        var checks = new SmokeChecks(host, loopback);
        SmokeCheck[] results =
        [
            await checks.CheckHostAsync(cancellationToken),
            checks.CheckProxy(),
            await checks.CheckShellAsync(cancellationToken),
            checks.CheckUpdates(),
            await checks.ProbeApiAsync(launch.ShouldProbeApi, cancellationToken),
        ];
        foreach (var result in results)
        {
            await output.WriteLineAsync(result.ToString());
        }

        var isHealthy = results.All(result => result.IsHealthy);
        await output.WriteLineAsync($"smoke: {(isHealthy ? "ok" : "failed")}");
        return isHealthy ? Healthy : Unhealthy;
    }
}
