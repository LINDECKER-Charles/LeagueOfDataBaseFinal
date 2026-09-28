using LoDb.Api.Modules.Profiles.Favorites;

namespace LoDb.Api.Tests.Profiles.Units;

public sealed class FavoriteSelectionTests
{
    private static readonly HashSet<string> Known = ["Ahri", "1001", "8100", "SummonerFlash"];

    [Fact]
    public void KnownIdsAreKeptTrimmed()
    {
        var submitted = new FavoriteIds
        {
            Champion = " Ahri ",
            Item = "1001",
            Rune = "8100",
            Summoner = "SummonerFlash",
        };

        var result = Sanitize(submitted, FavoriteIds.None);

        Assert.Equal(submitted with { Champion = "Ahri" }, result.Values);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public void UnknownIdIsRejectedUnlessItIsTheStoredOne()
    {
        var stored = new FavoriteIds { Champion = "Zed" };
        var submitted = new FavoriteIds { Champion = "Zed", Item = "9999" };

        var result = Sanitize(submitted, stored);

        Assert.Equal("Zed", result.Values.Champion);
        Assert.Null(result.Values.Item);
        Assert.Equal([FavoriteSlot.Item], result.Rejected);
    }

    [Fact]
    public void BlankSlotClearsWithoutBeingRejected()
    {
        var stored = new FavoriteIds { Champion = "Ahri", Item = "1001" };
        var submitted = new FavoriteIds { Champion = "   ", Item = null };

        var result = Sanitize(submitted, stored);

        Assert.Equal(FavoriteIds.None, result.Values);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public void IdLongerThanItsColumnIsRejectedEvenWhenStored()
    {
        var longItem = new string('1', FavoriteIds.MaxLength(FavoriteSlot.Item) + 1);
        var stored = new FavoriteIds { Item = longItem };

        var result = Sanitize(new FavoriteIds { Item = longItem }, stored);

        Assert.Null(result.Values.Item);
        Assert.Equal([FavoriteSlot.Item], result.Rejected);
    }

    private static SanitizedFavorites Sanitize(FavoriteIds submitted, FavoriteIds stored) =>
        FavoriteSelection.Sanitize(submitted, stored, static (_, id) => Known.Contains(id));
}
