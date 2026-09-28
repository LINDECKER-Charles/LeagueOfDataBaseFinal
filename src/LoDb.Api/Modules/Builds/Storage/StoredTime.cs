namespace LoDb.Api.Modules.Builds.Storage;

/// <summary>
/// The instant a build row records: the legacy columns keep whole seconds, so the answer
/// shows the value a later read returns.
/// </summary>
internal static class StoredTime
{
    public static DateTimeOffset Now(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
