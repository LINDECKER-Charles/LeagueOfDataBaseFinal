using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// A build carried over to another patch keeps what that patch still offers, drops the rest
/// and says so; the source stays as it was.
/// </summary>
[Collection(BuildsGroup.Name)]
public sealed class ImportTests(BuildsApp app)
{
    // Gone from the oldest recorded patches: Domination lost Sixth Sense (8137) and the item
    // list lacks the placeholder 7050.
    private const string OldPatch = "7.22.1";
    private const string NoRunesPatch = "7.21.1";

    // Of the recorded items, only the placeholder is offered on the Arena map.
    private const string ArenaSteps = """[{"label":"Start","note":null,"items":["7050","1001"]}]""";

    private const string StepsWithPlaceholder =
        """[{"label":"Start","note":null,"items":["7050"]},"""
        + """{"label":"Core","note":"Rush it","items":["3078","7050","3006"]}]""";

    [Fact]
    public async Task OlderPatchResetsTheRunesAndDropsWhatItLacks()
    {
        var account = await app.SeedAsync();
        var stored = new StoredBuild { Steps = StepsWithPlaceholder };
        var build = await app.InsertAsync(account.Username, stored);
        using var owner = await app.SignInAsync(account);

        var preview = await owner.GetJsonAsync(ImportOf(build.Id, OldPatch));

        var draft = preview.GetProperty("draft");
        Assert.Equal(OldPatch, ApiJson.Text(draft, "gameVersion"));
        Assert.False(draft.GetProperty("isPublic").GetBoolean());
        var structure = draft.GetProperty("structure");
        Assert.Equal(0, structure.GetProperty("runes").GetProperty("primaryStyleId").GetInt32());
        var step = Assert.Single(structure.GetProperty("steps").EnumerateArray());
        Assert.Equal("Core", ApiJson.Text(step, "label"));
        Assert.Equal(["3078", "3006"], Items(step));
        var report = preview.GetProperty("report");
        Assert.False(report.GetProperty("championMissing").GetBoolean());
        Assert.True(report.GetProperty("runesReset").GetBoolean());
        var dropped = report.GetProperty("droppedItems");
        Assert.Equal([0, 1], dropped.EnumerateArray().Select(static d => Field(d, "step")));
        Assert.All(dropped.EnumerateArray(), static d => Assert.Equal("7050", Text(d, "name")));
        var source = await app.FindBuildAsync(build.Id);
        Assert.Contains("7050", source?.Steps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchWithoutRunesResetsThem()
    {
        var account = await app.SeedAsync();
        var build = await app.InsertAsync(account.Username, new StoredBuild());
        using var owner = await app.SignInAsync(account);

        var preview = await owner.GetJsonAsync(ImportOf(build.Id, NoRunesPatch));

        Assert.True(preview.GetProperty("report").GetProperty("runesReset").GetBoolean());
        Assert.Empty(preview.GetProperty("report").GetProperty("droppedItems").EnumerateArray());
    }

    [Fact]
    public async Task PatchOfferingEverythingKeepsTheBuildWhole()
    {
        var account = await app.SeedAsync();
        var build = await app.InsertAsync(account.Username, new StoredBuild());
        using var owner = await app.SignInAsync(account);

        var preview = await owner.GetJsonAsync(ImportOf(build.Id, BuildsApp.Previous.Value));

        var report = preview.GetProperty("report");
        Assert.False(report.GetProperty("runesReset").GetBoolean());
        Assert.Empty(report.GetProperty("droppedItems").EnumerateArray());
        var runes = preview.GetProperty("draft").GetProperty("structure").GetProperty("runes");
        Assert.Equal([8126, 8137], runes.GetProperty("secondarySelections").EnumerateArray()
            .Select(static perk => perk.GetInt32()));
    }

    [Fact]
    public async Task ItemsOffTheModeMapAreDroppedByName()
    {
        var account = await app.SeedAsync();
        var stored = new StoredBuild { GameMode = "arena", Steps = ArenaSteps };
        var build = await app.InsertAsync(account.Username, stored);
        using var owner = await app.SignInAsync(account);

        var preview = await owner.GetJsonAsync(BuildCalls.Of(build.Id) + "/import");

        var draft = preview.GetProperty("draft");
        Assert.Equal(BuildsApp.Latest.Value, ApiJson.Text(draft, "gameVersion"));
        Assert.Equal("arena", ApiJson.Text(draft, "gameMode"));
        var dropped = Assert.Single(
            preview.GetProperty("report").GetProperty("droppedItems").EnumerateArray());
        Assert.Equal("1001", ApiJson.Text(dropped, "id"));
        Assert.Equal("Boots", ApiJson.Text(dropped, "name"));
    }

    [Fact]
    public async Task MissingChampionIsKeptAndFlagged()
    {
        var account = await app.SeedAsync();
        var stored = new StoredBuild { ChampionId = "Hwei" };
        var build = await app.InsertAsync(account.Username, stored);
        using var owner = await app.SignInAsync(account);

        var preview = await owner.GetJsonAsync(ImportOf(build.Id, BuildsApp.Latest.Value));

        Assert.True(preview.GetProperty("report").GetProperty("championMissing").GetBoolean());
        var structure = preview.GetProperty("draft").GetProperty("structure");
        Assert.Equal("Hwei", ApiJson.Text(structure, "championId"));
    }

    [Theory]
    [InlineData("seven", HttpStatusCode.BadRequest, "invalid-version")]
    [InlineData("99.1.1", HttpStatusCode.NotFound, "unknown-version")]
    public async Task TargetPatchMustExist(string to, HttpStatusCode status, string code)
    {
        var account = await app.SeedAsync();
        var build = await app.InsertAsync(account.Username, new StoredBuild());
        using var owner = await app.SignInAsync(account);

        using var response = await owner.GetAsync(ImportOf(build.Id, to));

        Assert.Equal(code, await ApiJson.ProblemCodeAsync(response, status));
    }

    [Fact]
    public async Task OnlyAVerifiedOwnerImports()
    {
        var owner = await app.SeedAsync(new AccountSeed { EmailConfirmed = false });
        var build = await app.InsertAsync(owner.Username, new StoredBuild());
        using var unverified = await app.SignInAsync(owner);
        using var other = await app.SignInAsync(await app.SeedAsync());

        using var refused = await unverified.GetAsync(ImportOf(build.Id, OldPatch));
        using var hidden = await other.GetAsync(ImportOf(build.Id, OldPatch));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    private static int Field(JsonElement element, string name) =>
        element.GetProperty(name).GetInt32();

    private static string? Text(JsonElement element, string name) => ApiJson.Text(element, name);

    private static string ImportOf(int id, string to) => $"{BuildCalls.Of(id)}/import?to={to}";

    private static string?[] Items(JsonElement step) =>
        [.. step.GetProperty("items").EnumerateArray().Select(static item => item.GetString())];
}
