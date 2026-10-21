namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// The versions of the running pass whose units wait (<see cref="UnitEnd.Waiting"/>), each set
/// aside until <see cref="RecheckAfter"/> has passed: the worker then tries it again between
/// two units, rather than once the rest of the pass is written.
/// </summary>
/// <remarks>
/// A full pass lasts days: a version promoted ahead of the others, or put first by the
/// operator, must be rendered as soon as it can be, not after every other unit. Its wait is
/// logged once (<see cref="UnitWaits"/>), however often it is tried again.
/// </remarks>
internal sealed class WaitingVersions(TimeProvider clock)
{
    /// <summary>
    /// How long a waiting version is set aside: the worker's own period, so that a version is
    /// checked as often whether other units run meanwhile or not.
    /// </summary>
    public static readonly TimeSpan RecheckAfter = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, DateTimeOffset> _until = new(StringComparer.Ordinal);

    /// <summary>Sets <paramref name="version"/> aside for <see cref="RecheckAfter"/>.</summary>
    public void Hold(string version) => _until[version] = clock.GetUtcNow() + RecheckAfter;

    /// <summary>The versions still set aside now.</summary>
    public IReadOnlyCollection<string> Held()
    {
        var now = clock.GetUtcNow();
        string[] held =
            [.. _until.Where(entry => entry.Value > now).Select(static entry => entry.Key)];
        return held;
    }
}
