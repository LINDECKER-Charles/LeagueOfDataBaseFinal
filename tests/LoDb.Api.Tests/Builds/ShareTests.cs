using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// Anyone holding the link reads the build, public or not, rendered on the patch it was made
/// for: what that patch lacks shows as a ghost.
/// </summary>
[Collection(BuildsGroup.Name)]
public sealed class ShareTests(BuildsApp app)
{
    private const string OldPatch = "7.22.1";

    [Fact]
    public async Task VisitorReadsAPublicBuildWithItsScore()
    {
        var author = await app.SeedAsync();
        await app.UpdateAsync(author.Username, static user =>
        {
            user.IsSupporter = true;
            user.IsPublicProfile = true;
            user.RiotTagline = "EUW1";
        });
        var build = await app.InsertAsync(author.Username, new StoredBuild { Name = "Shared" });
        using var visitor = app.Browser();

        using var response = await visitor.GetAsync(BuildCalls.Share + build.ShareToken);
        var shared = await ApiJson.ReadAsync(response, HttpStatusCode.OK);

        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("Shared", ApiJson.Text(shared, "name"));
        Assert.Equal(BuildsApp.Latest.Value, ApiJson.Text(shared, "currentVersion"));
        Assert.False(shared.GetProperty("patchMismatch").GetBoolean());
        var owner = shared.GetProperty("owner");
        Assert.Equal(author.Username, ApiJson.Text(owner, "username"));
        Assert.Equal("EUW1", ApiJson.Text(owner, "riotTagline"));
        Assert.True(owner.GetProperty("isSupporter").GetBoolean());
        Assert.True(owner.GetProperty("hasPublicProfile").GetBoolean());
        Assert.Equal("the Nine-Tailed Fox", ApiJson.Text(shared.GetProperty("champion"), "title"));
        var runes = shared.GetProperty("runes");
        Assert.Equal("Press the Attack", ApiJson.Text(runes.GetProperty("keystone"), "name"));
        Assert.Equal(3, runes.GetProperty("minors").GetArrayLength());
        Assert.Equal("Domination", ApiJson.Text(runes.GetProperty("secondary"), "name"));
        Assert.Equal([350, 4433], Golds(shared));
        Assert.Equal(4783, shared.GetProperty("totalGold").GetInt32());
        Assert.Equal(0, shared.GetProperty("vote").GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task PrivateBuildIsReadableByItsLinkWithoutVotes()
    {
        var author = await app.SeedAsync();
        var build = await app.InsertAsync(author.Username, new StoredBuild { IsPublic = false });
        using var visitor = app.Browser();

        var shared = await visitor.GetJsonAsync(BuildCalls.Share + build.ShareToken);

        Assert.False(shared.GetProperty("isPublic").GetBoolean());
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("vote").ValueKind);
    }

    [Fact]
    public async Task SignedInVoterSeesTheirVote()
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        using var voter = await app.SignInAsync(await app.SeedAsync());
        using var cast = await voter.VoteAsync(build.Id, "down");
        Assert.Equal(HttpStatusCode.OK, cast.StatusCode);

        var vote = (await voter.GetJsonAsync(BuildCalls.Share + build.ShareToken))
            .GetProperty("vote");

        Assert.Equal(-1, vote.GetProperty("score").GetInt32());
        Assert.Equal(-1, vote.GetProperty("myVote").GetInt32());
    }

    [Fact]
    public async Task OldBuildShowsWhatItsPatchLacksAsGhosts()
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild
        {
            GameVersion = OldPatch,
            Steps = """[{"label":"Core","note":null,"items":["3078","7050"]}]""",
        });
        using var visitor = app.Browser();

        var shared = await visitor.GetJsonAsync(BuildCalls.Share + build.ShareToken);

        Assert.Equal(OldPatch, ApiJson.Text(shared, "gameVersion"));
        Assert.True(shared.GetProperty("patchMismatch").GetBoolean());
        var perks = shared.GetProperty("runes").GetProperty("secondaryPerks");
        Assert.False(perks[0].GetProperty("missing").GetBoolean());
        Assert.True(perks[1].GetProperty("missing").GetBoolean());
        Assert.Equal("8137", ApiJson.Text(perks[1], "name"));
        var items = shared.GetProperty("steps")[0].GetProperty("items");
        Assert.False(items[0].GetProperty("missing").GetBoolean());
        Assert.True(items[1].GetProperty("missing").GetBoolean());
        Assert.Equal("7050", ApiJson.Text(items[1], "name"));
        Assert.Equal("absent", ApiJson.Text(items[1].GetProperty("image"), "status"));
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("gold").ValueKind);
    }

    [Fact]
    public async Task NamesFollowTheQueryThenTheBuildLanguage()
    {
        var author = (await app.SeedAsync()).Username;
        var french = await app.InsertAsync(author, new StoredBuild { Language = "fr_FR" });
        var english = await app.InsertAsync(author, new StoredBuild());
        using var visitor = app.Browser();

        var own = await visitor.GetJsonAsync(BuildCalls.Share + french.ShareToken);
        var asked = await visitor.GetJsonAsync(
            BuildCalls.Share + english.ShareToken + "?lang=fr_FR");

        Assert.Equal("Renarde à neuf queues", ApiJson.Text(own.GetProperty("champion"), "title"));
        var primary = asked.GetProperty("runes").GetProperty("primary");
        Assert.Equal("Précision", ApiJson.Text(primary, "name"));
    }

    [Fact]
    public async Task BannedAuthorKeepsTheLinkButNotTheProfile()
    {
        var author = await app.SeedAsync();
        await app.UpdateAsync(author.Username, static user =>
        {
            user.IsPublicProfile = true;
            user.IsBanned = true;
        });
        var build = await app.InsertAsync(author.Username, new StoredBuild());
        using var visitor = app.Browser();

        var shared = await visitor.GetJsonAsync(BuildCalls.Share + build.ShareToken);

        Assert.False(shared.GetProperty("owner").GetProperty("hasPublicProfile").GetBoolean());
    }

    [Theory]
    [InlineData("0123456789abcdef01234567")]
    [InlineData("0123456789ABCDEF01234567")]
    [InlineData("0123456789abcdef")]
    [InlineData("not-a-token")]
    public async Task UnknownOrMalformedTokenIsNotFound(string token)
    {
        using var visitor = app.Browser();

        using var response = await visitor.GetAsync(BuildCalls.Share + token);

        var code = await ApiJson.ProblemCodeAsync(response, HttpStatusCode.NotFound);
        Assert.Equal("build-not-found", code);
    }

    [Theory]
    [InlineData("?lang=zz", "invalid-language")]
    [InlineData("?version=seven", "invalid-version")]
    public async Task MalformedQueryIsABadRequest(string query, string code)
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        using var visitor = app.Browser();

        using var response = await visitor.GetAsync(BuildCalls.Share + build.ShareToken + query);

        Assert.Equal(code, await ApiJson.ProblemCodeAsync(response, HttpStatusCode.BadRequest));
    }

    private static int[] Golds(JsonElement shared) =>
        [.. shared.GetProperty("steps").EnumerateArray()
            .Select(static step => step.GetProperty("gold").GetInt32())];
}
