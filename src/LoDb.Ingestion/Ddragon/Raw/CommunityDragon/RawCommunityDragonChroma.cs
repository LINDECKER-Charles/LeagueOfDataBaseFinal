namespace LoDb.Ingestion.Ddragon.Raw.CommunityDragon;

/// <summary>
/// A chroma as CommunityDragon describes it; <c>chromaPath</c> is a game path
/// ("/lol-game-data/assets/v1/champion-chroma-images/103/103052.png").
/// </summary>
internal sealed record RawCommunityDragonChroma
{
    public int? Id { get; init; }

    public string? Name { get; init; }

    public string? ChromaPath { get; init; }

    public List<string?>? Colors { get; init; }
}
