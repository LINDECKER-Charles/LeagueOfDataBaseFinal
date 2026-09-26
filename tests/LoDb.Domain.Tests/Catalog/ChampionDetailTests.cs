using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Catalog;

/// <summary>
/// A champion page renders from the summary alone when its detail dataset is missing.
/// </summary>
public sealed class ChampionDetailTests
{
    [Fact]
    public void Up3DetailRendersFromTheSummaryAlone()
    {
        // Old versions answer 403 on champion/{id}.json while champion.json is there.
        var summary = ChampionSamples.Named("Ahri", "Ahri");

        var detail = ChampionDetail.FromSummary(summary);

        Assert.Same(summary, detail.Summary);
        Assert.Null(detail.Lore);
        Assert.Null(detail.Passive);
        Assert.Empty(detail.Spells);
        Assert.Empty(detail.Skins);
        Assert.Empty(detail.AllyTips);
        Assert.Empty(detail.EnemyTips);
    }

    [Fact]
    public void Up3PartypeMayBeMissing()
    {
        var summary = ChampionSamples.Named("Ahri", "Ahri");

        Assert.Null(summary.Partype);
        Assert.Null(summary.Info);
    }
}
