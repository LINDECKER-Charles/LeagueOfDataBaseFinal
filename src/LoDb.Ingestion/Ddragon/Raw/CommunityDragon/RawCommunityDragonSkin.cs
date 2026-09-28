namespace LoDb.Ingestion.Ddragon.Raw.CommunityDragon;

/// <summary>
/// A skin of CommunityDragon's <c>v1/skins.json</c>, reduced to its chromas.
/// </summary>
internal sealed record RawCommunityDragonSkin
{
    public int? Id { get; init; }

    public List<RawCommunityDragonChroma?>? Chromas { get; init; }
}
