using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;

namespace LoDb.Api.Tests.Builds;

/// <summary>A public profile lists the public builds of its owner, freshest first.</summary>
[Collection(BuildsGroup.Name)]
public sealed class PublicProfileBuildsTests(BuildsApp app)
{
    private const string Profiles = "/api/profiles/";

    [Fact]
    public async Task CardListsOnlyThePublicBuilds()
    {
        var author = await app.SeedAsync();
        await app.UpdateAsync(author.Username, static user => user.IsPublicProfile = true);
        var older = await app.InsertAsync(author.Username, new StoredBuild { Name = "Older" });
        var hidden = new StoredBuild { Name = "Hidden", IsPublic = false };
        await app.InsertAsync(author.Username, hidden);
        var newer = await app.InsertAsync(author.Username, new StoredBuild
        {
            Name = "Newer",
            ChampionId = "Garen",
            CreatedAt = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero),
        });
        using var visitor = app.Browser();

        var card = await visitor.GetJsonAsync(Profiles + author.Username);

        var builds = card.GetProperty("builds").EnumerateArray().ToList();
        Assert.Equal(
            [newer.ShareToken, older.ShareToken],
            builds.Select(static build => ApiJson.Text(build, "shareToken")));
        Assert.Equal("Garen", ApiJson.Text(builds[0], "championName"));
        Assert.Equal(BuildsApp.Latest.Value, ApiJson.Text(builds[0], "gameVersion"));
    }
}
