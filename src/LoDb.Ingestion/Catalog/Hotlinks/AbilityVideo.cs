namespace LoDb.Ingestion.Catalog.Hotlinks;

/// <summary>
/// The looping preview of one ability, in two formats and with its still frame.
/// </summary>
/// <remarks>
/// Not every ability has one: the page probes the video and falls back to the ability icon
/// when it fails to load.
/// </remarks>
public sealed record AbilityVideo
{
    public required Uri Webm { get; init; }

    public required Uri Mp4 { get; init; }

    /// <summary>The frame shown before the video plays (JPEG).</summary>
    public required Uri Poster { get; init; }
}
