using Microsoft.Extensions.Hosting;

namespace LoDb.Api.Tests.Workers.Fake;

/// <summary>
/// A background service found by the convention under this assembly's <c>Workers</c> folder.
/// </summary>
internal sealed class FakeWorker(FakeWorkerProbe probe) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        probe.Started.TrySetResult();
        return Task.CompletedTask;
    }
}
