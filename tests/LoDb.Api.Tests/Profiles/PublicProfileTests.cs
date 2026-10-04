using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;

namespace LoDb.Api.Tests.Profiles;

/// <summary>
/// The public card: shown when the profile is public, previewed by its owner when not, and
/// the same 404 whatever keeps it hidden.
/// </summary>
[Collection(ProfilesGroup.Name)]
public sealed class PublicProfileTests(ProfilesApp app)
{
    private const string NotFound = "profile-not-found";

    [Fact]
    public async Task PublicProfileShowsItsCardToAnyone()
    {
        var account = await ShowcasedAsync(isPublic: true);
        using var visitor = app.Browser();

        var card = await visitor.GetJsonAsync(ProfileCalls.Public + account.Username);

        Assert.Equal(account.Username, ApiJson.Text(card, "username"));
        Assert.Equal("EUW1", ApiJson.Text(card, "riotTagline"));
        Assert.True(card.GetProperty("isPublic").GetBoolean());
        var showcase = card.GetProperty("showcase");
        Assert.Equal("Garen", ProfileCalls.SlotName(showcase, "champion"));
        Assert.Equal("Sanguine Garen", ApiJson.Text(showcase.GetProperty("skin"), "name"));
        Assert.Equal("skin", ApiJson.Text(card.GetProperty("backdrop"), "kind"));
        Assert.Empty(card.GetProperty("builds").EnumerateArray());
        Assert.False(card.TryGetProperty("maskedEmail", out _));
    }

    [Fact]
    public async Task OwnerPreviewsAPrivateProfileOthersFindNothing()
    {
        var account = await ShowcasedAsync(isPublic: false);
        using var owner = await app.SignInAsync(account);
        using var other = await app.SignInAsync(await app.SeedAsync());
        using var visitor = app.Browser();

        var preview = await owner.GetJsonAsync(ProfileCalls.Preview);
        using var byOther = await other.GetAsync(ProfileCalls.Public + account.Username);
        using var byVisitor = await visitor.GetAsync(ProfileCalls.Public + account.Username);

        Assert.False(preview.GetProperty("isPublic").GetBoolean());
        Assert.Equal("Garen", ProfileCalls.SlotName(preview.GetProperty("showcase"), "champion"));
        Assert.Equal(NotFound, await ApiJson.ProblemCodeAsync(byOther, HttpStatusCode.NotFound));
        Assert.Equal(
            NotFound,
            await ApiJson.ProblemCodeAsync(byVisitor, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task HiddenProfilesAllAnswerTheSameNotFound()
    {
        var hidden = await ShowcasedAsync(isPublic: false);
        var banned = await ShowcasedAsync(isPublic: true);
        await app.BanAsync(banned.Username);
        using var visitor = app.Browser();
        string[] names = ["Personne_404", hidden.Username, banned.Username, "x", "a%20b"];

        var bodies = new List<string>();
        foreach (var name in names)
        {
            using var response = await visitor.GetAsync(ProfileCalls.Public + name);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ApiJson.ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore);
            bodies.Add(await ProfileCalls.UntracedBodyAsync(response));
        }

        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task BannedAccountLosesItsCardAndItsProfile()
    {
        var account = await ShowcasedAsync(isPublic: true);
        using var owner = await app.SignInAsync(account);
        await owner.GetJsonAsync(ProfileCalls.Own);

        await app.BanAsync(account.Username);
        using var visitor = app.Browser();
        using var card = await visitor.GetAsync(ProfileCalls.Public + account.Username);
        using var own = await owner.GetAsync(ProfileCalls.Own);

        Assert.Equal(NotFound, await ApiJson.ProblemCodeAsync(card, HttpStatusCode.NotFound));
        Assert.Equal(HttpStatusCode.Unauthorized, own.StatusCode);
    }

    [Fact]
    public async Task VisibilityIsSwitchedByItsOwner()
    {
        var account = await ShowcasedAsync(isPublic: false);
        using var owner = await app.SignInAsync(account);

        using var visitor = app.Browser();
        using var shown = await owner.PutAsync(ProfileCalls.Visibility, new { isPublic = true });
        using var card = await visitor.GetAsync(ProfileCalls.Public + account.Username);

        Assert.Equal(HttpStatusCode.NoContent, shown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        var audit = await app.AuditAsync(account.Username);
        Assert.Contains(audit, static entry => AccountsApp.Meta(entry, "section") == "visibility");
    }

    [Fact]
    public async Task SignedOutVisitorHasNoOwnProfile()
    {
        using var visitor = app.Browser();

        using var own = await visitor.GetAsync(ProfileCalls.Own);
        using var preview = await visitor.GetAsync(ProfileCalls.Preview);

        Assert.Equal(HttpStatusCode.Unauthorized, own.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, preview.StatusCode);
    }

    private async Task<AccountSeed> ShowcasedAsync(bool isPublic)
    {
        var account = await app.SeedAsync();
        await app.UpdateAsync(account.Username, user =>
        {
            user.IsPublicProfile = isPublic;
            user.RiotTagline = "EUW1";
            user.FavoriteChampionId = "Garen";
            user.FavoriteSkinId = "Garen_1";
        });
        return account;
    }
}
