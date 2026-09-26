using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Profiles;

/// <summary>
/// The favorites are checked and resolved on the version the profile reads; one the patch
/// lacks is shown as unavailable, never wiped.
/// </summary>
[Collection(ProfilesGroup.Name)]
public sealed class FavoritesTests(ProfilesApp app)
{
    private static readonly FavoritesBody Picked =
        new("Ahri", "1001", "8100", "SummonerFlash", "Ahri_1");

    [Fact]
    public async Task SavedFavoritesResolveOnTheLatestPatch()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var saved = await browser.SaveFavoritesAsync(Picked);
        var body = await ApiJson.ReadAsync(saved, HttpStatusCode.OK);

        Assert.Empty(body.GetProperty("rejected").EnumerateArray());
        Assert.False(body.GetProperty("isSkinRejected").GetBoolean());
        var profile = await browser.GetJsonAsync(ProfileCalls.Own);
        var showcase = profile.GetProperty("showcase");
        Assert.Equal(ProfilesApp.Latest.Value, ApiJson.Text(showcase, "version"));
        Assert.True(showcase.GetProperty("isCatalogAvailable").GetBoolean());
        Assert.Equal("resolved", ProfileCalls.SlotStatus(showcase, "champion"));
        Assert.Equal("Ahri", ProfileCalls.SlotName(showcase, "champion"));
        Assert.Equal("Domination", ProfileCalls.SlotName(showcase, "rune"));
        Assert.Equal("Flash", ProfileCalls.SlotName(showcase, "summoner"));
        Assert.Equal("Dynasty Ahri", ApiJson.Text(showcase.GetProperty("skin"), "name"));
        Assert.Equal("champion", ApiJson.Text(profile.GetProperty("backdrop"), "kind"));
        var audit = await app.AuditAsync(account.Username);
        Assert.Contains(audit, static entry => entry.Action == AuditAction.ProfileUpdate
            && AccountsApp.Meta(entry, "section") == "favorites");
    }

    [Fact]
    public async Task RuneIsFoundByItsTreeOrByItself()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());

        using var saved = await browser.SaveFavoritesAsync(new FavoritesBody(Rune: "8112"));
        await ApiJson.ReadAsync(saved, HttpStatusCode.OK);

        var showcase = (await browser.GetJsonAsync(ProfileCalls.Own)).GetProperty("showcase");
        Assert.Equal("Electrocute", ProfileCalls.SlotName(showcase, "rune"));
        Assert.Equal("empty", ProfileCalls.SlotStatus(showcase, "champion"));
    }

    [Fact]
    public async Task StoredFavoriteThePatchLacksIsKeptAndShownUnavailable()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        await app.UpdateAsync(account.Username, static user => user.FavoriteChampionId = "Zed");

        var before = await browser.GetJsonAsync(ProfileCalls.Own);
        using var saved = await browser.SaveFavoritesAsync(new FavoritesBody("Zed", "9999"));

        var favorite = before.GetProperty("showcase").GetProperty("favorites")
            .GetProperty("champion");
        Assert.Equal("unavailable", ApiJson.Text(favorite, "status"));
        Assert.Equal("Zed", ApiJson.Text(favorite, "storedId"));
        var body = await ApiJson.ReadAsync(saved, HttpStatusCode.OK);
        Assert.Equal(["item"], body.GetProperty("rejected").EnumerateArray()
            .Select(static slot => slot.GetString()));
        var stored = await app.FindAsync(account.Username);
        Assert.Equal("Zed", stored!.FavoriteChampionId);
        Assert.Null(stored.FavoriteItemId);
    }

    [Fact]
    public async Task BlankSlotClearsAndMalformedSkinIsRefused()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        using var first = await browser.SaveFavoritesAsync(Picked);
        await ApiJson.ReadAsync(first, HttpStatusCode.OK);

        using var second = await browser.SaveFavoritesAsync(
            Picked with { Champion = "  ", Skin = "Ahri 1" });

        var body = await ApiJson.ReadAsync(second, HttpStatusCode.OK);
        Assert.True(body.GetProperty("isSkinRejected").GetBoolean());
        var stored = await app.FindAsync(account.Username);
        Assert.Null(stored!.FavoriteChampionId);
        Assert.Null(stored.FavoriteSkinId);
        Assert.Equal("1001", stored.FavoriteItemId);
    }

    [Fact]
    public async Task SaveIsRefusedWhenThePinnedCatalogCannotBeRead()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        await app.UpdateAsync(account.Username, static user =>
        {
            user.FavoriteChampionId = "Garen";
            user.PreferredVersion = ProfilesApp.Broken.Value;
        });

        using var saved = await browser.SaveFavoritesAsync(Picked);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, saved.StatusCode);
        Assert.Equal("Garen", (await app.FindAsync(account.Username))!.FavoriteChampionId);
        var showcase = (await browser.GetJsonAsync(ProfileCalls.Own)).GetProperty("showcase");
        Assert.False(showcase.GetProperty("isCatalogAvailable").GetBoolean());
        Assert.Equal("unavailable", ProfileCalls.SlotStatus(showcase, "champion"));
    }

    [Fact]
    public async Task ProfileAnswersAreNeverCached()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());

        using var response = await browser.GetAsync(ProfileCalls.Own);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
