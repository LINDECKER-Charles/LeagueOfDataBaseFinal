using System.Runtime.CompilerServices;
using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// A disposed API host leaves nothing behind that keeps it alive: a test run starts hundreds
/// of hosts, and each one kept by a timer or a static registry stayed in memory to the end.
/// </summary>
public sealed class HostReleaseTests
{
    private const int Collections = 10;
    private static readonly TimeSpan BetweenCollections = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task ADisposedHostIsCollected()
    {
        // OpenTelemetry starts its self-diagnostics once per process, in the execution context
        // of the first host, which it then keeps for good: the host watched comes after it.
        await StartThenDisposeAsync();
        var services = await StartThenDisposeAsync();

        for (var collection = 0; collection < Collections && services.IsAlive; collection++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(BetweenCollections, TestContext.Current.CancellationToken);
        }

        Assert.False(services.IsAlive, "The services of the disposed host are still reachable.");
    }

    // Not inlined, so that no local of the test keeps the host reachable.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> StartThenDisposeAsync()
    {
        var factory = new ApiFactory();
        using (var client = factory.CreateClient())
        {
            using var response = await client.GetAsync(
                new Uri("/healthz", UriKind.Relative),
                TestContext.Current.CancellationToken);
        }

        var services = new WeakReference(factory.Services);
        await factory.DisposeAsync();
        return services;
    }
}
