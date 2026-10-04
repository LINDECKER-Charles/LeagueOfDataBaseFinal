using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Builds.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// A build is saved only by a verified account, whole or not at all: every refusal of its
/// fields comes in one answer, and the items its mode's map lacks are named.
/// </summary>
[Collection(BuildsGroup.Name)]
public sealed partial class CreateBuildTests(BuildsApp app)
{
    [Fact]
    public async Task VerifiedAccountCreatesANormalizedBuild()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var response = await browser.PostAsync(BuildCalls.Builds, BuildCalls.ValidBody());
        var build = await ApiJson.ReadAsync(response, HttpStatusCode.Created);

        var id = build.GetProperty("id").GetInt32();
        Assert.Equal(BuildCalls.Of(id), response.Headers.Location?.OriginalString);
        Assert.Equal("Ahri mid burst", ApiJson.Text(build, "name"));
        Assert.Equal("Roam after six.", ApiJson.Text(build, "description"));
        Assert.Equal(BuildsApp.Latest.Value, ApiJson.Text(build, "gameVersion"));
        Assert.Equal("sr", ApiJson.Text(build, "gameMode"));
        Assert.Equal("en_US", ApiJson.Text(build, "language"));
        Assert.Matches(ShareToken(), ApiJson.Text(build, "shareToken"));
        var structure = build.GetProperty("structure");
        Assert.Equal("Ahri", ApiJson.Text(structure, "championId"));
        var core = structure.GetProperty("steps")[1];
        Assert.Equal("Core", ApiJson.Text(core, "label"));
        Assert.Equal("Rush it", ApiJson.Text(core, "note"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        var stored = await app.FindBuildAsync(id);
        Assert.Equal("Ahri", stored?.ChampionId);
        Assert.Contains("\"secondarySelections\"", stored?.Runes, StringComparison.Ordinal);
        var audit = Assert.Single(await app.AuditAsync(account.Username));
        Assert.Equal(AuditAction.BuildCreate, audit.Action);
        Assert.Equal(id.ToString(CultureInfo.InvariantCulture), audit.TargetId);
        Assert.Equal("Ahri mid burst", audit.Target);
    }

    [Fact]
    public async Task BlankPatchAndLanguageTakeTheLatestAndTheEditorLanguage()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        var body = BuildCalls.ValidBody();
        body["gameVersion"] = " ";
        body.Remove("language");
        body.Remove("gameMode");

        using var response = await browser.PostAsync(BuildCalls.Builds + "?lang=fr_FR", body);
        var build = await ApiJson.ReadAsync(response, HttpStatusCode.Created);

        Assert.Equal(BuildsApp.Latest.Value, ApiJson.Text(build, "gameVersion"));
        Assert.Equal("fr_FR", ApiJson.Text(build, "language"));
        Assert.Equal("sr", ApiJson.Text(build, "gameMode"));
    }

    [Fact]
    public async Task UnverifiedAccountIsRefused()
    {
        var account = await app.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = await app.SignInAsync(account);

        using var response = await browser.PostAsync(BuildCalls.Builds, BuildCalls.ValidBody());

        var code = await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal("email-not-verified", code);
        Assert.Empty(await app.AuditAsync(account.Username));
    }

    [Fact]
    public async Task SignedOutVisitorIsRefused()
    {
        using var visitor = app.Browser();

        using var response = await visitor.PostAsync(BuildCalls.Builds, BuildCalls.ValidBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EveryMetadataRefusalComesInOneAnswer()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        var body = BuildCalls.ValidBody();
        body["name"] = " ab ";
        body["description"] = new string('d', 2001);
        body["gameVersion"] = "99.1.1";
        body["language"] = "xx_XX";

        using var response = await browser.PostAsync(BuildCalls.Builds, body);
        var errors = await ApiJson.FieldErrorsAsync(response);

        Assert.Equal(["name.length"], errors["name"]);
        Assert.Equal(["description.length"], errors["description"]);
        Assert.Equal(["version.unknown"], errors["gameVersion"]);
        Assert.Equal(["language.unknown"], errors["language"]);
        Assert.False(errors.ContainsKey("structure"));
    }

    [Fact]
    public async Task MissingStructureIsInvalid()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        var body = BuildCalls.ValidBody();
        body.Remove("structure");

        using var response = await browser.PostAsync(BuildCalls.Builds, body);

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["structure.invalid"], errors["structure"]);
    }

    [Fact]
    public async Task StructureRefusalsAreTheValidatorCodes()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        var body = BuildCalls.ValidBody();
        var structure = BuildCalls.Structure(body);
        structure["championId"] = "Nobody";
        structure["steps"] = new JsonArray(BuildCalls.Step(new string('l', 41), null, "999999"));

        using var response = await browser.PostAsync(BuildCalls.Builds, body);

        var codes = (await ApiJson.FieldErrorsAsync(response))["structure"];
        Assert.Contains("champion.unknown", codes);
        Assert.Contains("steps.label", codes);
        Assert.Contains("steps.item_unknown", codes);
        Assert.DoesNotContain("steps.item_mode", codes);
    }

    [Fact]
    public async Task ItemsTheModeMapLacksAreNamedInTheEditorLanguage()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        var body = BuildCalls.ValidBody();
        body["gameMode"] = "arena";
        BuildCalls.Structure(body)["steps"] = new JsonArray(
            BuildCalls.Step("Start", null, "7050", "1001"));

        using var english = await browser.PostAsync(BuildCalls.Builds, body);
        using var french = await browser.PostAsync(BuildCalls.Builds + "?lang=fr_FR", body);

        Assert.Equal(["Boots"], await UnavailableAsync(english));
        Assert.Equal(["Bottes"], await UnavailableAsync(french));
    }

    [Fact]
    public async Task ClassicItemsAreRefusedAndQualified()
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        var body = BuildCalls.ValidBody();
        body["gameMode"] = "aram";
        BuildCalls.Structure(body)["steps"] = new JsonArray(
            BuildCalls.Step("Start", null, "771004", "2003"));

        using var response = await browser.PostAsync(BuildCalls.Builds, body);

        Assert.Equal(["Faerie Charm [771004]"], await UnavailableAsync(response));
    }

    [Theory]
    [InlineData("\"gameMode\":\"urf\"")]
    [InlineData("\"isPublic\":\"yes\"")]
    public async Task MalformedBodyIsABadRequest(string field)
    {
        using var browser = await app.SignInAsync(await app.SeedAsync());
        using var request = browser.Post(BuildCalls.Builds);
        request.Content = new StringContent(
            $$"""{"name":"Valid name",{{field}}}""",
            Encoding.UTF8,
            "application/json");

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [GeneratedRegex("^[a-f0-9]{24}$")]
    private static partial Regex ShareToken();

    private static async Task<string?[]> UnavailableAsync(HttpResponseMessage response)
    {
        var problem = await ApiJson.ReadAsync(response, HttpStatusCode.BadRequest);
        var codes = problem.GetProperty("errors").GetProperty("structure").EnumerateArray()
            .Select(static code => code.GetString());
        Assert.Contains("steps.item_mode", codes);
        return [.. problem.GetProperty("unavailableItems").EnumerateArray()
            .Select(static name => name.GetString())];
    }
}
