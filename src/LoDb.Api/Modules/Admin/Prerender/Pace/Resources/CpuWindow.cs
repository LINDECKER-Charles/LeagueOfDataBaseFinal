using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// A <see cref="CpuTally"/> noted every 30 s over 10 minutes: the cores a group of servers
/// used and the renders it ended over the last minute, and its CPU per render over the window.
/// </summary>
internal sealed class CpuWindow(TimeProvider clock)
{
    /// <summary>Notes in the window: 10 minutes at one every 30 s.</summary>
    public const int Notes = 20;

    /// <summary>Notes in a minute.</summary>
    public const int MinuteNotes = 2;

    private const double SecondsPerMinute = 60;

    private readonly SampleRing<Note> _notes = new(Notes + 1);

    public void Add(CpuTally tally)
    {
        ArgumentNullException.ThrowIfNull(tally);
        _notes.Add(new Note(clock.GetTimestamp(), tally.CpuSeconds, tally.Renders));
    }

    /// <summary>Cores used over the last minute; null before two notes.</summary>
    public double? Cores() =>
        Delta(MinuteNotes) is { } delta && delta.Seconds > 0 ? delta.Cpu / delta.Seconds : null;

    /// <summary>Renders ended a minute over the last minute; null before two notes.</summary>
    public double? RendersPerMinute() =>
        Delta(MinuteNotes) is { } delta && delta.Seconds > 0
            ? delta.Renders * SecondsPerMinute / delta.Seconds
            : null;

    /// <summary>
    /// CPU seconds per render over the window; null under <paramref name="rendersNeeded"/>
    /// renders, too few to tell a render from the idle server.
    /// </summary>
    public double? CpuPerRender(int rendersNeeded) =>
        Delta(Notes) is { } delta && delta.Renders >= rendersNeeded && delta.Renders > 0
            ? delta.Cpu / delta.Renders
            : null;

    private (double Cpu, long Renders, double Seconds)? Delta(int steps)
    {
        if (_notes.Span(steps) is not var (earlier, latest))
        {
            return null;
        }

        var seconds = clock.GetElapsedTime(earlier.At, latest.At).TotalSeconds;
        return (latest.Cpu - earlier.Cpu, latest.Renders - earlier.Renders, seconds);
    }

    private sealed record Note(long At, double Cpu, long Renders);
}
