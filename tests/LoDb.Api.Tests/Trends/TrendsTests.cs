using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;
using LoDb.Api.Tests.Trends.Support;

namespace LoDb.Api.Tests.Trends;

/// <summary>
/// The public builds, best net score first then freshest, 24 a page: private builds and those
/// of banned authors never enter.
/// </summary>
/// <remarks>
/// Each test files its builds under a champion of its own and filters on it, so the others'
/// builds never enter its ranking.
/// </remarks>
[Collection(TrendsGroup.Name)]
public sealed class TrendsTests(BuildsApp app)
{
    [Fact]
    public async Task RanksByNetScoreThenFreshness()
    {
        var author = (await app.SeedAsync()).Username;
        var loved = await app.InsertAsync(author, Dated("Ranked", 1));
        var older = await app.InsertAsync(author, Dated("Ranked", 2));
        var newer = await app.InsertAsync(author, Dated("Ranked", 3));
        var hated = await app.InsertAsync(author, Dated("Ranked", 4));
        await app.InsertVotesAsync(loved.Id, 1, 1, -1);
        await app.InsertVotesAsync(older.Id, 1, -1);
        await app.InsertVotesAsync(hated.Id, -1);
        using var visitor = app.Browser();

        using var response = await visitor.GetAsync(BuildCalls.Trends + "?champion=Ranked");
        var page = await ApiJson.ReadAsync(response, HttpStatusCode.OK);

        Assert.True(response.Headers.CacheControl?.NoStore);
        var rows = page.GetProperty("rows");
        Assert.Equal([loved.Id, newer.Id, older.Id, hated.Id], BuildCalls.Ids(rows));
        Assert.Equal([1, 0, 0, -1], rows.EnumerateArray().Select(Score));
        Assert.Equal(4, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("pages").GetInt32());
        Assert.Equal(24, page.GetProperty("perPage").GetInt32());
        Assert.Equal("Ranked", ApiJson.Text(page.GetProperty("filters"), "champion"));
    }

    [Fact]
    public async Task PagesHoldTwentyFourBuilds()
    {
        var author = (await app.SeedAsync()).Username;
        for (var day = 1; day <= 25; day++)
        {
            await app.InsertAsync(author, Dated("Paged", day));
        }

        using var visitor = app.Browser();

        var first = await visitor.GetJsonAsync(BuildCalls.Trends + "?champion=Paged&page=0");
        var second = await visitor.GetJsonAsync(BuildCalls.Trends + "?champion=Paged&page=2");
        var beyond = await visitor.GetJsonAsync(BuildCalls.Trends + "?champion=Paged&page=9");

        Assert.Equal(1, first.GetProperty("page").GetInt32());
        Assert.Equal(24, first.GetProperty("rows").GetArrayLength());
        Assert.Equal(2, first.GetProperty("pages").GetInt32());
        Assert.Equal(25, first.GetProperty("total").GetInt32());
        var last = Assert.Single(second.GetProperty("rows").EnumerateArray());
        var oldest = Dated("Paged", 1).CreatedAt;
        Assert.Equal(oldest, last.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(0, beyond.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task PrivateBuildsAndBannedAuthorsNeverEnter()
    {
        var author = await app.SeedAsync();
        await app.UpdateAsync(author.Username, static user => user.IsSupporter = true);
        var banned = await app.SeedAsync(new AccountSeed { Banned = true });
        var listed = await app.InsertAsync(author.Username, Dated("Screened", 1));
        await app.InsertAsync(author.Username, Dated("Screened", 2) with { IsPublic = false });
        await app.InsertAsync(banned.Username, Dated("Screened", 3));
        await app.InsertAsync(banned.Username, Dated("Outcast", 3) with { Language = "ko_KR" });
        using var visitor = app.Browser();

        var page = await visitor.GetJsonAsync(BuildCalls.Trends + "?champion=Screened");

        var row = Assert.Single(page.GetProperty("rows").EnumerateArray());
        Assert.Equal(listed.Id, row.GetProperty("id").GetInt32());
        var owner = row.GetProperty("owner");
        Assert.Equal(author.Username, ApiJson.Text(owner, "username"));
        Assert.True(owner.GetProperty("isSupporter").GetBoolean());
        Assert.DoesNotContain("Outcast", Options(page, "championOptions", "id"));
        Assert.DoesNotContain("ko_KR", Options(page, "languageOptions", null));
    }

    [Fact]
    public async Task FiltersOnModeAndLanguage()
    {
        var author = (await app.SeedAsync()).Username;
        var aram = await app.InsertAsync(author, Dated("Filtered", 1) with { GameMode = "aram" });
        var french = await app.InsertAsync(
            author,
            Dated("Filtered", 2) with { Language = "fr_FR" });
        var plain = await app.InsertAsync(author, Dated("Filtered", 3));
        using var visitor = app.Browser();
        const string Query = BuildCalls.Trends + "?champion=Filtered";

        var byMode = await visitor.GetJsonAsync(Query + "&mode=aram");
        var byLanguage = await visitor.GetJsonAsync(Query + "&language=fr_FR");
        var unknown = await visitor.GetJsonAsync(Query + "&mode=urf&language=xx_XX");

        Assert.Equal([aram.Id], BuildCalls.Ids(byMode.GetProperty("rows")));
        Assert.Equal("aram", ApiJson.Text(byMode.GetProperty("filters"), "mode"));
        Assert.Equal([french.Id], BuildCalls.Ids(byLanguage.GetProperty("rows")));
        Assert.Equal(
            [plain.Id, french.Id, aram.Id],
            BuildCalls.Ids(unknown.GetProperty("rows")));
        var filters = unknown.GetProperty("filters");
        Assert.Equal(JsonValueKind.Null, filters.GetProperty("mode").ValueKind);
        Assert.Equal(JsonValueKind.Null, filters.GetProperty("language").ValueKind);
        Assert.Contains("fr_FR", Options(unknown, "languageOptions", null));
    }

    [Fact]
    public async Task RowsShowTheChampionTheKeystoneAndSixItems()
    {
        var author = (await app.SeedAsync()).Username;
        await app.InsertAsync(author, Dated("Teemo", 1) with
        {
            Steps = """[{"label":"A","items":["1001","2003","2003","2003"]},"""
                + """{"label":"B","items":["3078","3006","7050"]}]""",
        });
        using var visitor = app.Browser();

        var page = await visitor.GetJsonAsync(BuildCalls.Trends + "?champion=Teemo&lang=fr_FR");

        var row = Assert.Single(page.GetProperty("rows").EnumerateArray());
        Assert.Equal("Teemo", ApiJson.Text(row.GetProperty("champion"), "name"));
        Assert.Equal("Attaque soutenue", ApiJson.Text(row.GetProperty("keystone"), "name"));
        var items = row.GetProperty("items").EnumerateArray()
            .Select(static item => ApiJson.Text(item, "id"));
        Assert.Equal(["1001", "2003", "2003", "2003", "3078", "3006"], items);
        Assert.Contains("Teemo", Options(page, "championOptions", "name"));
    }

    [Fact]
    public async Task SignedInVoterSeesTheirVote()
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, Dated("Voted", 1));
        using var voter = await app.SignInAsync(await app.SeedAsync());
        using var cast = await voter.VoteAsync(build.Id, "up");
        Assert.Equal(HttpStatusCode.OK, cast.StatusCode);
        using var visitor = app.Browser();

        var mine = await voter.GetJsonAsync(BuildCalls.Trends + "?champion=Voted");
        var theirs = await visitor.GetJsonAsync(BuildCalls.Trends + "?champion=Voted");

        Assert.Equal(1, mine.GetProperty("rows")[0].GetProperty("myVote").GetInt32());
        Assert.Equal(0, theirs.GetProperty("rows")[0].GetProperty("myVote").GetInt32());
        Assert.Equal(1, Score(theirs.GetProperty("rows")[0]));
    }

    [Theory]
    [InlineData("?lang=zz", HttpStatusCode.BadRequest)]
    [InlineData("?version=seven", HttpStatusCode.BadRequest)]
    [InlineData("?version=99.1.1", HttpStatusCode.NotFound)]
    public async Task CatalogOfTheNamesMustExist(string query, HttpStatusCode status)
    {
        using var visitor = app.Browser();

        using var response = await visitor.GetAsync(BuildCalls.Trends + query);

        Assert.Equal(status, response.StatusCode);
    }

    // A public build of its own champion, a day later than the one before it.
    private static StoredBuild Dated(string champion, int day) => new()
    {
        ChampionId = champion,
        CreatedAt = new DateTimeOffset(2025, 1, day, 0, 0, 0, TimeSpan.Zero),
    };

    private static int Score(JsonElement row) => row.GetProperty("score").GetInt32();

    private static IReadOnlyList<string?> Options(JsonElement page, string facet, string? field) =>
        [.. page.GetProperty(facet).EnumerateArray()
            .Select(option => field is null ? option.GetString() : ApiJson.Text(option, field))];
}
