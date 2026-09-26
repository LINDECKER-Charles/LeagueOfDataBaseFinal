using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// Only its owner reads a build for editing, changes or deletes it; to anyone else it does
/// not exist.
/// </summary>
[Collection(BuildsGroup.Name)]
public sealed class EditBuildTests(BuildsApp app)
{
    private const string BuildNotFound = "build-not-found";

    [Fact]
    public async Task OwnerReadsTheBuildAsSaved()
    {
        using var owner = await app.SignInAsync(await app.SeedAsync());
        var created = await owner.CreateAsync(BuildCalls.ValidBody());

        var read = await owner.GetJsonAsync(BuildCalls.Of(created));

        Assert.Equal(created.GetRawText(), read.GetRawText());
    }

    [Fact]
    public async Task OwnerUpdatesTheBuildAndKeepsItsToken()
    {
        var account = await app.SeedAsync();
        using var owner = await app.SignInAsync(account);
        var created = await owner.CreateAsync(BuildCalls.ValidBody());
        var body = BuildCalls.ValidBody();
        body["name"] = "Ahri support";
        body["isPublic"] = false;
        body["gameMode"] = "aram";

        using var response = await owner.SendAsync(HttpMethod.Put, BuildCalls.Of(created), body);
        var updated = await ApiJson.ReadAsync(response, HttpStatusCode.OK);

        Assert.Equal("Ahri support", ApiJson.Text(updated, "name"));
        Assert.False(updated.GetProperty("isPublic").GetBoolean());
        Assert.Equal("aram", ApiJson.Text(updated, "gameMode"));
        Assert.Equal(ApiJson.Text(created, "shareToken"), ApiJson.Text(updated, "shareToken"));
        Assert.Equal(ApiJson.Text(created, "createdAt"), ApiJson.Text(updated, "createdAt"));
        var audit = await app.AuditAsync(account.Username);
        Assert.Equal(
            [AuditAction.BuildCreate, AuditAction.BuildUpdate],
            audit.Select(static entry => entry.Action));
        Assert.Equal("Ahri support", audit[^1].Target);
    }

    [Fact]
    public async Task RefusedUpdateChangesNothing()
    {
        var account = await app.SeedAsync();
        using var owner = await app.SignInAsync(account);
        var created = await owner.CreateAsync(BuildCalls.ValidBody());
        var body = BuildCalls.ValidBody();
        body["name"] = "x";

        using var response = await owner.SendAsync(HttpMethod.Put, BuildCalls.Of(created), body);

        Assert.Equal(["name.length"], (await ApiJson.FieldErrorsAsync(response))["name"]);
        var stored = await app.FindBuildAsync(created.GetProperty("id").GetInt32());
        Assert.Equal("Ahri mid burst", stored?.Name);
        Assert.Single(await app.AuditAsync(account.Username));
    }

    [Fact]
    public async Task OwnerDeletesTheBuild()
    {
        var account = await app.SeedAsync();
        using var owner = await app.SignInAsync(account);
        var created = await owner.CreateAsync(BuildCalls.ValidBody());
        var id = created.GetProperty("id").GetInt32();

        using var response = await owner.SendAsync(HttpMethod.Delete, BuildCalls.Of(id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await app.FindBuildAsync(id));
        var audit = await app.AuditAsync(account.Username);
        Assert.Equal(AuditAction.BuildDelete, audit[^1].Action);
        Assert.Equal("Ahri mid burst", audit[^1].Target);
        using var again = await owner.GetAsync(BuildCalls.Of(id));
        Assert.Equal(BuildNotFound, await NotFoundCodeAsync(again));
    }

    [Fact]
    public async Task AnotherAccountFindsNothing()
    {
        using var owner = await app.SignInAsync(await app.SeedAsync());
        var created = await owner.CreateAsync(BuildCalls.ValidBody());
        var path = BuildCalls.Of(created);
        var account = await app.SeedAsync();
        using var other = await app.SignInAsync(account);

        using var read = await other.GetAsync(path);
        using var update = await other.SendAsync(HttpMethod.Put, path, BuildCalls.ValidBody());
        using var delete = await other.SendAsync(HttpMethod.Delete, path);

        Assert.Equal(BuildNotFound, await NotFoundCodeAsync(read));
        Assert.Equal(BuildNotFound, await NotFoundCodeAsync(update));
        Assert.Equal(BuildNotFound, await NotFoundCodeAsync(delete));
        Assert.NotNull(await app.FindBuildAsync(created.GetProperty("id").GetInt32()));
        Assert.Empty(await app.AuditAsync(account.Username));
    }

    [Fact]
    public async Task UnknownBuildIsNotFound()
    {
        using var owner = await app.SignInAsync(await app.SeedAsync());

        using var response = await owner.GetAsync(BuildCalls.Of(int.MaxValue));

        Assert.Equal(BuildNotFound, await NotFoundCodeAsync(response));
    }

    [Fact]
    public async Task UnverifiedOwnerStillReadsAndDeletesButCannotUpdate()
    {
        var account = await app.SeedAsync(new AccountSeed { EmailConfirmed = false });
        var stored = await app.InsertAsync(account.Username, new StoredBuild());
        using var owner = await app.SignInAsync(account);

        using var update = await owner.SendAsync(
            HttpMethod.Put, BuildCalls.Of(stored.Id), BuildCalls.ValidBody());
        using var read = await owner.GetAsync(BuildCalls.Of(stored.Id));
        using var delete = await owner.SendAsync(HttpMethod.Delete, BuildCalls.Of(stored.Id));

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task SignedOutVisitorIsRefused()
    {
        using var visitor = app.Browser();

        using var read = await visitor.GetAsync(BuildCalls.Of(1));
        using var mine = await visitor.GetAsync(BuildCalls.Builds);

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mine.StatusCode);
    }

    private static Task<string?> NotFoundCodeAsync(HttpResponseMessage response) =>
        ApiJson.ProblemCodeAsync(response, HttpStatusCode.NotFound);
}
