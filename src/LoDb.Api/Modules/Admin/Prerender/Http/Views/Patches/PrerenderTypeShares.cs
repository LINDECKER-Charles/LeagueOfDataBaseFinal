namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>Shares from 0 to 1, summing to 1, to four decimals.</summary>
internal sealed record PrerenderTypeShares
{
    public required decimal Champions { get; init; }

    public required decimal Items { get; init; }

    public required decimal Runes { get; init; }

    public required decimal Summoners { get; init; }
}
