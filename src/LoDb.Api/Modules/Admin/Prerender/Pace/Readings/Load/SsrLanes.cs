namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

/// <summary>The renders of each lane of an SSR's admission.</summary>
internal sealed record SsrLanes
{
    public required SsrLaneLoad Current { get; init; }

    public required SsrLaneLoad Pinned { get; init; }

    public required SsrLaneLoad Prerender { get; init; }
}
