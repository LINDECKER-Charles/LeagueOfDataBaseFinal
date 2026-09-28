using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// The list of one's builds, freshest first, each on its own patch: what a patch lost is a
/// ghost, never dropped.
/// </summary>
[Collection(BuildsGroup.Name)]
public sealed class MyBuildsTests(BuildsApp app)
{
    [Fact]
    public async Task ListsOnlyMyBuildsFreshestFirst()
    {
        var account = await app.SeedAsync();
        var older = await app.InsertAsync(account.Username, new StoredBuild { Name = "Older" });
        var newer = await app.InsertAsync(account.Username, new StoredBuild
        {
            Name = "Newer",
            IsPublic = false,
            CreatedAt = new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero),
        });
        await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        using var owner = await app.SignInAsync(account);

        var rows = await owner.GetJsonAsync(BuildCalls.Builds);

        Assert.Equal([newer.Id, older.Id], BuildCalls.Ids(rows));
        var first = rows[0];
        Assert.False(first.GetProperty("isPublic").GetBoolean());
        Assert.Equal("sr", ApiJson.Text(first, "gameMode"));
        Assert.Equal(newer.ShareToken, ApiJson.Text(first, "shareToken"));
        var champion = first.GetProperty("champion");
        Assert.Equal("Ahri", ApiJson.Text(champion, "name"));
        Assert.False(champion.GetProperty("missing").GetBoolean());
        Assert.Equal("Press the Attack", ApiJson.Text(first.GetProperty("keystone"), "name"));
    }

    [Fact]
    public async Task NamesFollowTheRequestedLanguage()
    {
        var account = await app.SeedAsync();
        await app.InsertAsync(account.Username, new StoredBuild());
        using var owner = await app.SignInAsync(account);

        var rows = await owner.GetJsonAsync(BuildCalls.Builds + "?lang=fr_FR");

        Assert.Equal("Attaque soutenue", ApiJson.Text(rows[0].GetProperty("keystone"), "name"));
    }

    [Fact]
    public async Task WhatThePatchLacksIsAGhost()
    {
        var account = await app.SeedAsync();
        await app.InsertAsync(account.Username, new StoredBuild
        {
            ChampionId = "Hwei",
            Runes = StoredBuild.ValidRunes.Replace("8005", "9923", StringComparison.Ordinal),
        });
        using var owner = await app.SignInAsync(account);

        var row = (await owner.GetJsonAsync(BuildCalls.Builds))[0];

        var champion = row.GetProperty("champion");
        Assert.True(champion.GetProperty("missing").GetBoolean());
        Assert.Equal("Hwei", ApiJson.Text(champion, "name"));
        Assert.Equal("absent", ApiJson.Text(champion.GetProperty("image"), "status"));
        var keystone = row.GetProperty("keystone");
        Assert.True(keystone.GetProperty("missing").GetBoolean());
        Assert.Equal("9923", ApiJson.Text(keystone, "name"));
    }

    [Fact]
    public async Task EmptyListForANewAccount()
    {
        using var owner = await app.SignInAsync(await app.SeedAsync());

        using var response = await owner.GetAsync(BuildCalls.Builds);

        var rows = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(0, rows.GetArrayLength());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("?version=latest", HttpStatusCode.BadRequest)]
    [InlineData("?lang=zz", HttpStatusCode.BadRequest)]
    [InlineData("?version=99.1.1", HttpStatusCode.NotFound)]
    public async Task CatalogTheCallNamesMustExist(string query, HttpStatusCode status)
    {
        using var owner = await app.SignInAsync(await app.SeedAsync());

        using var response = await owner.GetAsync(BuildCalls.Builds + query);

        Assert.Equal(status, response.StatusCode);
    }
}
