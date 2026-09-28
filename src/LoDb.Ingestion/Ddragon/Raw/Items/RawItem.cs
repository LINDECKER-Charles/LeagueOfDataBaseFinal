namespace LoDb.Ingestion.Ddragon.Raw.Items;

/// <summary>
/// An item of <c>item.json</c>; its id is the key of the <c>data</c> map. 0.x versions write
/// null <c>plaintext</c> and null recipe ids.
/// </summary>
internal sealed record RawItem
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Plaintext { get; init; }

    public RawImage? Image { get; init; }

    public RawItemGold? Gold { get; init; }

    public List<string?>? From { get; init; }

    public List<string?>? Into { get; init; }

    public int? Depth { get; init; }

    public Dictionary<string, double?>? Stats { get; init; }

    public Dictionary<string, bool?>? Maps { get; init; }

    public List<string?>? Tags { get; init; }

    public bool? Consumed { get; init; }

    public bool? HideFromAll { get; init; }

    public string? RequiredChampion { get; init; }

    public string? RequiredAlly { get; init; }
}
