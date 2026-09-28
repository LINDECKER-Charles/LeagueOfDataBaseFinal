using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;

namespace LoDb.Api.Tests.Profiles;

/// <summary>The settings of the profile page: the pinned version and a first password.</summary>
[Collection(ProfilesGroup.Name)]
public sealed class AccountSettingsTests(ProfilesApp app)
{
    [Fact]
    public async Task ListedVersionIsPinnedAndBlankUnpins()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        var pinned = ProfilesApp.Latest.Value;

        using var pin = await browser.PutAsync(ProfileCalls.Version, new { version = pinned });
        var profile = await browser.GetJsonAsync(ProfileCalls.Own);
        using var unpin = await browser.PutAsync(ProfileCalls.Version, new { version = " " });

        Assert.Equal(HttpStatusCode.NoContent, pin.StatusCode);
        Assert.Equal(pinned, ApiJson.Text(profile, "preferredVersion"));
        Assert.Equal(pinned, ApiJson.Text(profile.GetProperty("showcase"), "version"));
        Assert.Equal(HttpStatusCode.NoContent, unpin.StatusCode);
        Assert.Null((await app.FindAsync(account.Username))!.PreferredVersion);
        var audit = await app.AuditAsync(account.Username);
        Assert.Contains(
            audit,
            static entry => AccountsApp.Meta(entry, "section") == "preferred_version");
    }

    [Theory]
    [InlineData("latest", "version-invalid")]
    [InlineData("16.x.1", "version-invalid")]
    [InlineData("99.1.1", "version-unknown")]
    public async Task VersionDataDragonDoesNotListIsRefused(string version, string code)
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var refused = await browser.PutAsync(ProfileCalls.Version, new { version });

        var errors = await ApiJson.FieldErrorsAsync(refused);
        Assert.Equal([code], errors["version"]);
    }

    [Fact]
    public async Task AccountWithoutPasswordSetsOneAndKeepsItsSession()
    {
        var account = await app.SeedAsync(new AccountSeed { GoogleId = "google-set-password" });
        using var browser = await app.SignInAsync(account);
        await app.UpdateAsync(account.Username, static user => user.PasswordHash = null);
        var before = await browser.GetJsonAsync(ProfileCalls.Own);

        using var weak = await browser.PutAsync(ProfileCalls.Password, new { password = "court" });
        const string Chosen = "Autre-phrase-s3crete!";
        using var set = await browser.PutAsync(ProfileCalls.Password, new { password = Chosen });

        Assert.False(before.GetProperty("hasPassword").GetBoolean());
        Assert.Contains("password", (await ApiJson.FieldErrorsAsync(weak)).Keys);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        var after = await browser.GetJsonAsync(ProfileCalls.Own);
        Assert.True(after.GetProperty("hasPassword").GetBoolean());
        using var other = app.Browser();
        using var signIn = await other.SignInAsync(account.Username, Chosen);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task PasswordIsNotReplacedThroughThisRoute()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        using var refused = await browser.PutAsync(
            ProfileCalls.Password,
            new { password = "Autre-phrase-s3crete!" });

        Assert.Equal(
            "password-exists",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task OwnProfileMasksTheAddress()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);

        var profile = await browser.GetJsonAsync(ProfileCalls.Own);

        Assert.Equal("j***@example.test", ApiJson.Text(profile, "maskedEmail"));
        Assert.Equal("password", ApiJson.Text(profile, "deletionConfirmation"));
        Assert.False(profile.GetProperty("isPublic").GetBoolean());
    }
}
