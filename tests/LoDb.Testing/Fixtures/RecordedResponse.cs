namespace LoDb.Testing.Fixtures;

/// <summary>
/// One recorded upstream answer of <c>tests/fixtures/ddragon/index.json</c>.
/// </summary>
public sealed record RecordedResponse
{
    public required string Url { get; init; }

    /// <summary>200, or the 403/404 of a definitive absence.</summary>
    public required int Status { get; init; }

    public string? ContentType { get; init; }

    /// <summary>
    /// A 200 recorded without its body: only its existence matters (hotlinked art, UP 8).
    /// It replays as an empty body.
    /// </summary>
    public bool Bodyless { get; init; }

    /// <summary>What the recorder cut from the upstream file, when it cut anything.</summary>
    public string? Reduction { get; init; }
}
