using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;

namespace LoDb.Api.Tests.Profiles;

/// <summary>
/// The summoner identity: a free username, and a Riot tag line of 3 to 5 characters.
/// </summary>
[Collection(ProfilesGroup.Name)]
public sealed class IdentityTests(ProfilesApp app)
{
    [Fact]
    public async Task RenameAndTaglineAreSavedTrimmed()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        var renamed = account.Username + "b";

        using var saved = await browser.PutAsync(
            ProfileCalls.Identity,
            new { username = $" {renamed} ", riotTagline = " EUW1 " });

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var stored = await app.FindAsync(renamed);
        Assert.Equal("EUW1", stored!.RiotTagline);
        var profile = await browser.GetJsonAsync(ProfileCalls.Own);
        Assert.Equal(renamed, ApiJson.Text(profile, "username"));
        var audit = await app.AuditAsync(account.Username);
        Assert.Contains(audit, static entry => AccountsApp.Meta(entry, "section") == "identity");
    }

    [Fact]
    public async Task BlankTaglineRemovesIt()
    {
        var account = await app.SeedAsync();
        await app.UpdateAsync(account.Username, static user => user.RiotTagline = "EUW1");
        using var browser = await app.SignInAsync(account);

        using var saved = await browser.PutAsync(
            ProfileCalls.Identity,
            new { username = account.Username, riotTagline = "  " });

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Null((await app.FindAsync(account.Username))!.RiotTagline);
    }

    [Fact]
    public async Task TaglineOutsideRiotsFormIsRefused()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var refused = await browser.PutAsync(
            ProfileCalls.Identity,
            new { username = account.Username, riotTagline = "EU-1" });

        var errors = await ApiJson.FieldErrorsAsync(refused);
        Assert.Equal(["tagline-invalid"], errors["riotTagline"]);
        Assert.Null((await app.FindAsync(account.Username))!.RiotTagline);
    }

    // Every rule of the username is IdentityRulesTests': one refusal proves the endpoint
    // applies them.
    [Fact]
    public async Task UsernameOutsideTheRulesIsRefused()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var refused = await browser.PutAsync(
            ProfileCalls.Identity,
            new { username = "a", riotTagline = (string?)null });

        var errors = await ApiJson.FieldErrorsAsync(refused);
        Assert.Equal(["username-invalid"], errors["username"]);
        Assert.NotNull(await app.FindAsync(account.Username));
    }

    [Fact]
    public async Task UsernameOfAnotherAccountIsRefusedWhateverItsCase()
    {
        var other = await app.SeedAsync();
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var refused = await browser.PutAsync(
            ProfileCalls.Identity,
            new { username = other.Username.ToUpperInvariant(), riotTagline = (string?)null });

        var errors = await ApiJson.FieldErrorsAsync(refused);
        Assert.Equal(["username-taken"], errors["username"]);
        Assert.NotNull(await app.FindAsync(account.Username));
    }
}
