using LoDb.Api.Modules.Seo.IndexNow;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Tests.Seo.States;

/// <summary>
/// What a promotion announces to IndexNow beyond the short URLs, read in the index of states
/// (<see cref="SeoStates"/>): the pages of changes that changed something, the openers of the
/// states closed since the previous latest patch, and the requests the whole takes.
/// </summary>
public sealed class IndexNowPlanTests
{
    private const string Origin = "https://league-of-data-base.com";

    // The previous latest patch unknown: the covered version before the promoted one.
    private static readonly PromotionIndex Index = new(SeoStates.Snapshot(), PreviousLatest: null);

    private static readonly int Locales = UiLocales.All.Count;

    [Fact]
    public void AnnouncesThePagesOfChangesOfThePromotedAndOfThePreviousPatch()
    {
        var pages = IndexNowPlan.PatchPagesOf(Origin, Version(SeoStates.Latest), Index);

        Assert.Equal(2 * Locales, pages.Count);
        Assert.Equal($"{Origin}/ar/changes", pages[0]);
        Assert.Equal($"{Origin}/ar/16.18.1/changes", pages[1]);
        Assert.Contains($"{Origin}/fr/changes", pages);
    }

    [Fact]
    public void LeavesOutAPageOfChangesThatChangedNothing()
    {
        var afterUnchanged = IndexNowPlan.PatchPagesOf(Origin, Version(SeoStates.Archived), Index);
        var unchanged = IndexNowPlan.PatchPagesOf(Origin, Version(SeoStates.Unchanged), Index);
        var start = IndexNowPlan.PatchPagesOf(Origin, Version(SeoStates.Start), Index);

        Assert.Equal(Locales, afterUnchanged.Count);
        Assert.All(afterUnchanged, static url =>
            Assert.EndsWith("/changes", url, StringComparison.Ordinal));
        Assert.DoesNotContain($"{Origin}/fr/7.22.1/changes", afterUnchanged);
        Assert.Empty(unchanged);
        Assert.Empty(start);
    }

    [Fact]
    public void AnnouncesTheOpenersThePromotionClosedUnderTheirVersion()
    {
        var openers = IndexNowPlan.OpenersOf(Origin, Version(SeoStates.Latest), Index);

        Assert.Equal(Locales, openers.Count);
        Assert.Equal($"{Origin}/ar/16.18.1/items/3078-trinity-force", openers[0]);
        Assert.Equal($"{Origin}/zh-hant/16.18.1/items/3078-trinity-force", openers[^1]);
    }

    [Fact]
    public void AnnouncesTheStatesClosedByAChangeAndByARetirement()
    {
        // The promotion of 16.18.1 closed Boots, changed there, and Molten Edge, gone there.
        var openers = IndexNowPlan.OpenersOf(Origin, Version(SeoStates.Previous), Index);

        Assert.Equal(2 * Locales, openers.Count);
        Assert.Contains($"{Origin}/fr/7.21.1/items/1001-boots", openers);
        Assert.Contains($"{Origin}/fr/8.7.1/items/3371-molten-edge", openers);
        Assert.Empty(IndexNowPlan.OpenersOf(Origin, Version(SeoStates.Start), Index));
    }

    [Fact]
    public void AnnouncesEveryVersionListedSinceThePreviousLatestPatch()
    {
        // 8.7.1 was the latest; 16.18.1 was listed, never promoted, before 16.19.1.
        var skipped = Index with { PreviousLatest = Version(SeoStates.Archived) };
        var latest = Version(SeoStates.Latest);

        var pages = IndexNowPlan.PatchPagesOf(Origin, latest, skipped);
        var openers = IndexNowPlan.OpenersOf(Origin, latest, skipped);

        Assert.Equal(
            [$"{Origin}/ar/changes", $"{Origin}/ar/16.18.1/changes", $"{Origin}/ar/8.7.1/changes"],
            pages.Take(3));
        Assert.Equal(3 * Locales, pages.Count);
        Assert.Equal(
            [
                $"{Origin}/ar/7.21.1/items/1001-boots",
                $"{Origin}/ar/8.7.1/items/3371-molten-edge",
                $"{Origin}/ar/16.18.1/items/3078-trinity-force",
            ],
            openers.Take(3));
        Assert.Equal(3 * Locales, openers.Count);
    }

    [Fact]
    public void ReadsTheCoveredVersionBeforeWhenThePreviousLatestIsNotOlder()
    {
        var latest = Version(SeoStates.Latest);
        var notOlder = Index with { PreviousLatest = latest };

        Assert.Equal(
            IndexNowPlan.OpenersOf(Origin, latest, Index),
            IndexNowPlan.OpenersOf(Origin, latest, notOlder));
        Assert.Equal(
            IndexNowPlan.PatchPagesOf(Origin, latest, Index),
            IndexNowPlan.PatchPagesOf(Origin, latest, notOlder));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10_000, 1)]
    [InlineData(10_001, 2)]
    [InlineData(25_000, 3)]
    public void SendsTenThousandUrlsAtMostPerRequest(int urls, int requests)
    {
        // Short URLs first: a refusal drops what follows.
        var shortUrls = Enumerable.Range(0, urls - Math.Min(urls, 2))
            .Select(static number => $"{Origin}/en/items/{number}")
            .ToList();
        string[] pages = [.. Enumerable.Repeat($"{Origin}/en/changes", Math.Min(urls, 1))];
        string[] openers = [.. Enumerable.Repeat($"{Origin}/en/8.7.1/items/1", Math.Min(urls, 1))];

        var plan = new IndexNowPlan(shortUrls, pages, openers);

        Assert.Equal(urls, plan.UrlCount);
        Assert.Equal(requests, plan.RequestCount);
        Assert.Equal([.. shortUrls, .. pages, .. openers], plan.Urls);
    }

    private static PatchVersion Version(string version) => PatchVersion.Parse(version);
}
