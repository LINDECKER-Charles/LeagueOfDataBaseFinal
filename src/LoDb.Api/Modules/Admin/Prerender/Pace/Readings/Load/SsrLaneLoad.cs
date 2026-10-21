namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

/// <summary>
/// One lane's renders since the SSR started: those that ended, their total duration, and the
/// refusals for the server's or the lane's sake (never a client's own share).
/// </summary>
internal sealed record SsrLaneLoad
{
    public required long Renders { get; init; }

    public required double RenderSeconds { get; init; }

    public required long Refused { get; init; }
}
