namespace LoDb.Ingestion.Ddragon.Raw;

/// <summary>
/// The <c>image</c> node of an entry: only the file name matters, the sprite coordinates do
/// not.
/// </summary>
internal sealed record RawImage
{
    public string? Full { get; init; }
}
