namespace LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

/// <summary>
/// The regulator's log lines: one per change of state, never one per evaluation nor per page
/// (ADR 0003, ADR 0010). The rules of increase and decrease bound them to a few an hour.
/// </summary>
internal static partial class PaceLog
{
    [LoggerMessage(
        EventName = "prerender.pace.slowed",
        Level = LogLevel.Information,
        Message = "The pre-rendering pass slows to {RendersPerMinute} renders a minute:"
            + " {Reason} at {Value}.")]
    public static partial void Slowed(
        ILogger logger,
        int rendersPerMinute,
        string reason,
        string value);

    [LoggerMessage(
        EventName = "prerender.pace.held",
        Level = LogLevel.Warning,
        Message = "The pre-rendering pass starts no render until {Until}, at least: {Reason} at"
            + " {Value}. Its pages in flight end.")]
    public static partial void Held(
        ILogger logger,
        string reason,
        string value,
        DateTimeOffset until);

    [LoggerMessage(
        EventName = "prerender.pace.recovered",
        Level = LogLevel.Information,
        Message = "The pre-rendering pass resumes at {RendersPerMinute} renders a minute after"
            + " {Seconds} s slowed or held.")]
    public static partial void Recovered(ILogger logger, int rendersPerMinute, long seconds);

    [LoggerMessage(
        EventName = "prerender.pace.ceiling_reached",
        Level = LogLevel.Information,
        Message = "The pre-rendering pass reaches its ceiling of {RendersPerMinute} renders a"
            + " minute.")]
    public static partial void CeilingReached(ILogger logger, int rendersPerMinute);

    [LoggerMessage(
        EventName = "prerender.pace.fixed",
        Level = LogLevel.Information,
        Message = "The pre-rendering pass keeps its fixed pace of {RendersPerMinute} renders a"
            + " minute: the regulation is off (LoDb:Prerender:Pace:Mode).")]
    public static partial void Fixed(ILogger logger, int rendersPerMinute);

    [LoggerMessage(
        EventName = "prerender.pace.setting_applied",
        Level = LogLevel.Information,
        Message = "The pre-rendering pace is now {Mode}, its manual target {RendersPerMinute}"
            + " renders a minute (set from the admin).")]
    public static partial void SettingApplied(ILogger logger, string mode, int rendersPerMinute);

    [LoggerMessage(
        EventName = "prerender.pace.signal_lost",
        Level = LogLevel.Warning,
        Message = "The pace regulator cannot read {Source}: the pass keeps to its floor while it"
            + " lasts.")]
    public static partial void SignalLost(ILogger logger, PaceSource source);

    [LoggerMessage(
        EventName = "prerender.pace.signal_restored",
        Level = LogLevel.Information,
        Message = "The pace regulator reads {Source} again after {Seconds} s.")]
    public static partial void SignalRestored(ILogger logger, PaceSource source, long seconds);

    [LoggerMessage(
        EventName = "prerender.pace.tick_failed",
        Level = LogLevel.Error,
        Message = "The pace regulator failed a tick; it samples again at the next one.")]
    public static partial void TickFailed(ILogger logger, Exception exception);
}
