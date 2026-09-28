using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Profiles.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Builds;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Tests.Profiles;

/// <summary>
/// The erasure of an account, confirmed as the account allows: its password, the phrase of
/// the page's locale for a Google account, nothing for an account with neither.
/// </summary>
[Collection(ProfilesGroup.Name)]
public sealed class DeletionTests(ProfilesApp app)
{
    [Fact]
    public async Task PasswordAccountIsErasedWithItsBuildsOnceConfirmed()
    {
        var account = await app.SeedAsync();
        var id = (await app.FindAsync(account.Username))!.Id;
        await AddBuildAsync(id);
        using var browser = await app.SignInAsync(account);

        using var wrong = await browser.PostAsync(ProfileCalls.Delete, new { password = "faux" });
        Assert.Equal(["password-incorrect"], (await ApiJson.FieldErrorsAsync(wrong))["password"]);
        using var erased = await browser.PostAsync(
            ProfileCalls.Delete,
            new { password = AccountSeed.StrongPassword });

        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Null(await app.FindAsync(account.Username));
        Assert.Equal(0, await CountBuildsAsync(id));
        Assert.Null(await browser.SessionUserAsync());
        var audit = await app.AuditAsync(account.Username);
        var line = Assert.Single(audit, static entry => entry.Action == AuditAction.AccountDelete);
        Assert.Equal(id.ToString(CultureInfo.InvariantCulture), line.TargetId);
    }

    [Theory]
    [InlineData("fr", " supprimer mon compte ")]
    [InlineData("en", "DELETE MY ACCOUNT")]
    [InlineData("de", "delete my account")]
    [InlineData(null, "DELETE MY ACCOUNT")]
    public async Task GoogleAccountTypesThePhraseOfItsLocale(string? locale, string phrase)
    {
        var account = await app.SeedAsync(new AccountSeed { GoogleId = GoogleId() });
        using var browser = await app.SignInAsync(account);
        var profile = await browser.GetJsonAsync(ProfileCalls.Own);

        using var erased = await browser.PostAsync(
            ProfileCalls.Delete,
            new { confirmation = phrase, locale });

        Assert.Equal("phrase", ApiJson.Text(profile, "deletionConfirmation"));
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Null(await app.FindAsync(account.Username));
    }

    [Theory]
    [InlineData("fr", "DELETE MY ACCOUNT")]
    [InlineData("en", "SUPPRIMER MON COMPTE")]
    [InlineData("fr", "")]
    public async Task GoogleAccountWithTheWrongPhraseStays(string locale, string phrase)
    {
        var account = await app.SeedAsync(new AccountSeed { GoogleId = GoogleId() });
        using var browser = await app.SignInAsync(account);

        using var refused = await browser.PostAsync(
            ProfileCalls.Delete,
            new { confirmation = phrase, locale, password = AccountSeed.StrongPassword });

        var errors = await ApiJson.FieldErrorsAsync(refused);
        Assert.Equal(["confirmation-incorrect"], errors["confirmation"]);
        Assert.NotNull(await app.FindAsync(account.Username));
    }

    [Fact]
    public async Task AccountWithNeitherPasswordNorGoogleNeedsNoConfirmation()
    {
        var account = await app.SeedAsync();
        using var browser = await app.SignInAsync(account);
        await app.UpdateAsync(account.Username, static user => user.PasswordHash = null);
        var profile = await browser.GetJsonAsync(ProfileCalls.Own);

        using var erased = await browser.PostAsync(ProfileCalls.Delete, new { });

        Assert.Equal("none", ApiJson.Text(profile, "deletionConfirmation"));
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Null(await app.FindAsync(account.Username));
    }

    [Fact]
    public async Task ErasedAccountHasNoProfileLeft()
    {
        var account = await app.SeedAsync();
        await app.UpdateAsync(account.Username, static user => user.IsPublicProfile = true);
        using var browser = await app.SignInAsync(account);
        using var visitor = app.Browser();

        using var erased = await browser.PostAsync(
            ProfileCalls.Delete,
            new { password = AccountSeed.StrongPassword });
        using var own = await browser.GetAsync(ProfileCalls.Own);
        using var card = await visitor.GetAsync(ProfileCalls.Public + account.Username);

        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, card.StatusCode);
    }

    // Google subjects are 21 digits; the column holds 30.
    private static string GoogleId() => Guid.NewGuid().ToString("N")[..21];

    private Task AddBuildAsync(int ownerId) =>
        app.WithContextAsync(async context =>
        {
            context.Builds.Add(new Build
            {
                Name = "Ahri mid",
                ChampionId = "Ahri",
                GameVersion = ProfilesApp.Latest.Value,
                Runes = "{}",
                Steps = "[]",
                ShareToken = "tok" + ownerId.ToString(CultureInfo.InvariantCulture),
                GameMode = "sr",
                Language = "en_US",
                OwnerId = ownerId,
            });
            await context.SaveChangesAsync(ProfilesApp.Token);
        });

    private async Task<int> CountBuildsAsync(int ownerId)
    {
        var count = 0;
        await app.WithContextAsync(async context =>
            count = await context.Builds.CountAsync(
                build => build.OwnerId == ownerId,
                ProfilesApp.Token));
        return count;
    }
}
