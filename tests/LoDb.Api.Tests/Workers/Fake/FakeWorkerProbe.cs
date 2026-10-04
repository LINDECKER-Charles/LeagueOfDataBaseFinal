namespace LoDb.Api.Tests.Workers.Fake;

/// <summary>
/// Completed by the fake background service when the host starts it.
/// </summary>
internal sealed class FakeWorkerProbe
{
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
