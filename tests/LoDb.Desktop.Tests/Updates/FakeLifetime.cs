using Microsoft.Extensions.Hosting;

namespace LoDb.Desktop.Tests.Updates;

/// <summary>A host lifetime that records the stop requests and stops nothing.</summary>
internal sealed class FakeLifetime : IHostApplicationLifetime
{
    public int StopCount { get; private set; }

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => StopCount++;
}
