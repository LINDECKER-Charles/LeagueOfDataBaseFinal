using System.Diagnostics;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>Waits for what a background consumer does, polling a condition.</summary>
internal static class Eventually
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);

    public static Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken) =>
        UntilAsync(() => Task.FromResult(condition()), cancellationToken);

    public static async Task UntilAsync(
        Func<Task<bool>> condition,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (!await condition())
        {
            if (elapsed.Elapsed > Deadline)
            {
                Assert.Fail("The condition still fails after 30 seconds.");
            }

            await Task.Delay(Poll, cancellationToken);
        }
    }
}
