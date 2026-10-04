using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// One vote per account and public build: the same vote again withdraws it, the other one
/// switches it, and only the net score is shown.
/// </summary>
[Collection(BuildsGroup.Name)]
public sealed class VoteTests(BuildsApp app)
{
    [Fact]
    public async Task VoteIsCastSwitchedAndWithdrawn()
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        var account = await app.SeedAsync();
        using var voter = await app.SignInAsync(account);

        Assert.Equal((1, 1), await VoteAsync(voter, build.Id, "up"));
        Assert.Equal((-1, -1), await VoteAsync(voter, build.Id, "down"));
        Assert.Equal((0, 0), await VoteAsync(voter, build.Id, "down"));

        Assert.Empty(await app.VotesAsync(build.Id));
        var audit = await app.AuditAsync(account.Username);
        Assert.All(audit, entry => Assert.Equal(AuditAction.BuildVote, entry.Action));
        Assert.Equal([1, -1, -1], audit.Select(VoteValue));
        var target = build.Id.ToString(CultureInfo.InvariantCulture);
        Assert.All(audit, entry => Assert.Equal(target, entry.TargetId));
    }

    [Fact]
    public async Task ScoreIsTheNetOfEveryVoter()
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        using var first = await app.SignInAsync(await app.SeedAsync());
        using var second = await app.SignInAsync(await app.SeedAsync());
        using var third = await app.SignInAsync(await app.SeedAsync());

        await VoteAsync(first, build.Id, "up");
        await VoteAsync(second, build.Id, "up");

        Assert.Equal((1, -1), await VoteAsync(third, build.Id, "down"));
        var stored = await app.VotesAsync(build.Id);
        Assert.Equal(1, stored.Sum(static vote => vote.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UP")]
    [InlineData("1")]
    public async Task OnlyUpOrDownIsAVote(string? value)
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        using var voter = await app.SignInAsync(await app.SeedAsync());

        using var response = await voter.VoteAsync(build.Id, value);

        Assert.Equal(["vote.invalid"], (await ApiJson.FieldErrorsAsync(response))["value"]);
        Assert.Empty(await app.VotesAsync(build.Id));
    }

    [Fact]
    public async Task PrivateOrUnknownBuildIsNotFound()
    {
        var owner = await app.SeedAsync();
        var hidden = await app.InsertAsync(owner.Username, new StoredBuild { IsPublic = false });
        using var voter = await app.SignInAsync(await app.SeedAsync());
        using var author = await app.SignInAsync(owner);

        using var other = await voter.VoteAsync(hidden.Id, "up");
        using var own = await author.VoteAsync(hidden.Id, "up");
        using var unknown = await voter.VoteAsync(int.MaxValue, "up");

        var code = await ApiJson.ProblemCodeAsync(other, HttpStatusCode.NotFound);
        Assert.Equal("build-not-found", code);
        Assert.Equal(HttpStatusCode.NotFound, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty(await app.VotesAsync(hidden.Id));
    }

    [Fact]
    public async Task SignedOutVisitorCannotVote()
    {
        var build = await app.InsertAsync((await app.SeedAsync()).Username, new StoredBuild());
        using var visitor = app.Browser();

        using var response = await visitor.VoteAsync(build.Id, "up");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await app.VotesAsync(build.Id));
    }

    [Fact]
    public async Task VotesGoWithTheirBuild()
    {
        var author = await app.SeedAsync();
        var build = await app.InsertAsync(author.Username, new StoredBuild());
        using var voter = await app.SignInAsync(await app.SeedAsync());
        await VoteAsync(voter, build.Id, "up");
        using var owner = await app.SignInAsync(author);

        using var deleted = await owner.SendAsync(HttpMethod.Delete, BuildCalls.Of(build.Id));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await app.VotesAsync(build.Id));
    }

    private static async Task<(int Score, int MyVote)> VoteAsync(
        BrowserClient voter,
        int buildId,
        string value)
    {
        using var response = await voter.VoteAsync(buildId, value);
        var state = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        return (state.GetProperty("score").GetInt32(), state.GetProperty("myVote").GetInt32());
    }

    private static int VoteValue(AuditLogEntry entry)
    {
        using var meta = JsonDocument.Parse(entry.Meta ?? "{}");
        return meta.RootElement.GetProperty("value").GetInt32();
    }
}
