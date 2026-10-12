using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>POST /api/account/register</c>: the account is created, signed in and sent its
/// verification e-mail; every broken rule is reported at once, a taken e-mail or name
/// whatever its case. The limit by address is <see cref="OwnHost.RegistrationLimitTests"/>'.
/// </summary>
/// <remarks>
/// The tests share a host: each registers from an address and under a name of its own.
/// </remarks>
public sealed class RegistrationTests(SharedAccountsApp host) : IClassFixture<SharedAccountsApp>
{
    private const string RegisterPath = "/api/account/register";

    private readonly AccountSeed _account = host.Unique();

    // The account a test registers: named as no other test of the host.
    private readonly AccountSeed _newcomer = host.Unique();
    private readonly string _address = host.NextAddress();

    private AccountsApp App { get; } = host.App;

    private string Username => _newcomer.Username;

    private string Email => _newcomer.Email;

    [Fact]
    public async Task RegistrationSignsInAndQueuesTheVerificationEmail()
    {
        using var browser = Browser();

        using var response = await browser.PostAsync(RegisterPath, new
        {
            email = $" {Email.ToUpperInvariant()} ",
            username = Username,
            password = AccountSeed.StrongPassword,
            acceptTerms = true,
            locale = "fr",
        });

        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.Created)).GetProperty("user");
        Assert.Equal("/api/account/me", response.Headers.Location?.OriginalString);
        Assert.Equal(Email, ApiJson.Text(user, "email"));
        Assert.False(user.GetProperty("emailVerified").GetBoolean());
        Assert.Equal(Username, await browser.SignedInAsAsync());
        var mail = Assert.Single(App.Outbox.To(Email));
        Assert.Equal((EmailTemplate.ConfirmEmail, UiLocale.Fr), (mail.Template, mail.Locale));
        var link = EmailLink.Of(mail);
        Assert.Equal(new Uri("https://localhost/fr/account/verify-email"), link.Page);
        Assert.Equal(user.GetProperty("id").GetInt32(), link.UserId);
        Assert.Equal("60", mail.Model[EmailModelKeys.ExpiresInMinutes]);
        Assert.Equal(Username, mail.Model[EmailModelKeys.UserName]);
    }

    [Fact]
    public async Task RegistrationIsAuditedOnTheNewAccount()
    {
        using var browser = Browser();

        using var response = await browser.PostAsync(RegisterPath, Registration(Email, Username));

        var id = (await ApiJson.ReadAsync(response, HttpStatusCode.Created))
            .GetProperty("user").GetProperty("id").GetInt32();
        var entry = Assert.Single(await App.AuditOfAsync(Username));
        Assert.Equal(
            (AuditAction.UserRegister, AuditActorType.User, (int?)id),
            (entry.Action, entry.ActorType, entry.ActorId));
        Assert.Equal(
            (id.ToString(CultureInfo.InvariantCulture), Username),
            (entry.TargetId, entry.Target));
    }

    [Fact]
    public async Task EveryBrokenRuleIsReportedAtOnce()
    {
        using var browser = Browser();

        using var response = await browser.PostAsync(RegisterPath, new
        {
            email = "nouveau@localhost",
            username = "_x",
            password = "password",
            acceptTerms = false,
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["email-invalid"], errors["email"]);
        Assert.Equal(["username-invalid"], errors["username"]);
        Assert.Contains("auth.password.rule_length", errors["password"]);
        Assert.Equal(["terms-required"], errors["acceptTerms"]);
        Assert.Empty(App.Outbox.To("nouveau@localhost"));
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    [Fact]
    public async Task TakenEmailAndUsernameAreRefusedWhateverTheirCase()
    {
        await App.SeedAsync(_account);
        using var browser = Browser();

        using var response = await browser.PostAsync(RegisterPath, Registration(
            email: _account.Email.ToUpperInvariant(),
            username: _account.Username.ToLowerInvariant()));

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["email-taken"], errors["email"]);
        Assert.Equal(["username-taken"], errors["username"]);
        Assert.Equal(
            ["1"],
            await App.QueryAsync(
                $"SELECT count(*) FROM users WHERE lower(email) = '{_account.Email}'"));
    }

    private static object Registration(string email, string username) => new
    {
        email,
        username,
        password = AccountSeed.StrongPassword,
        acceptTerms = true,
    };

    // Registrations are limited by address: this test's own.
    private BrowserClient Browser() => App.Browser().From(_address);
}
