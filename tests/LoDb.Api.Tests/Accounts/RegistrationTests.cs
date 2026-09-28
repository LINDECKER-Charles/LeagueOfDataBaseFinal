using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>POST /api/account/register</c>: the account is created, signed in and sent its
/// verification e-mail; every broken rule is reported at once, a taken e-mail or name
/// whatever its case, and one address registers five accounts an hour at most.
/// </summary>
public sealed class RegistrationTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string RegisterPath = "/api/account/register";
    private const string ForwardedFor = "X-Forwarded-For";
    private const int RegistrationsPerHour = 5;

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task RegistrationSignsInAndQueuesTheVerificationEmail()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(RegisterPath, new
        {
            email = " Nouveau@Example.TEST ",
            username = "Nouveau_7",
            password = AccountSeed.StrongPassword,
            acceptTerms = true,
            locale = "fr",
        });

        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.Created)).GetProperty("user");
        Assert.Equal("/api/account/me", response.Headers.Location?.OriginalString);
        Assert.Equal("nouveau@example.test", ApiJson.Text(user, "email"));
        Assert.False(user.GetProperty("emailVerified").GetBoolean());
        Assert.Equal("Nouveau_7", await browser.SignedInAsAsync());
        var mail = Assert.Single(App.Outbox.Messages);
        Assert.Equal(
            ("nouveau@example.test", EmailTemplate.ConfirmEmail, UiLocale.Fr),
            (mail.Recipient, mail.Template, mail.Locale));
        var link = EmailLink.Of(mail);
        Assert.Equal(new Uri("https://localhost/fr/account/verify-email"), link.Page);
        Assert.Equal(user.GetProperty("id").GetInt32(), link.UserId);
        Assert.Equal("60", mail.Model[EmailModelKeys.ExpiresInMinutes]);
        Assert.Equal("Nouveau_7", mail.Model[EmailModelKeys.UserName]);
    }

    [Fact]
    public async Task RegistrationIsAuditedOnTheNewAccount()
    {
        using var browser = App.Browser();

        using var response = await browser.PostAsync(RegisterPath, Registration());

        var id = (await ApiJson.ReadAsync(response, HttpStatusCode.Created))
            .GetProperty("user").GetProperty("id").GetInt32();
        var entry = Assert.Single(await App.AuditAsync());
        Assert.Equal(
            (AuditAction.UserRegister, AuditActorType.User, (int?)id),
            (entry.Action, entry.ActorType, entry.ActorId));
        Assert.Equal(
            (id.ToString(CultureInfo.InvariantCulture), "Nouveau_7"),
            (entry.TargetId, entry.Target));
    }

    [Fact]
    public async Task EveryBrokenRuleIsReportedAtOnce()
    {
        using var browser = App.Browser();

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
        Assert.Empty(App.Outbox.Messages);
        Assert.Null(browser.Cookie(BrowserClient.SessionCookie));
    }

    [Fact]
    public async Task TakenEmailAndUsernameAreRefusedWhateverTheirCase()
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var response = await browser.PostAsync(RegisterPath, Registration(
            email: "LEGENDE@example.test",
            username: "legende_42"));

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["email-taken"], errors["email"]);
        Assert.Equal(["username-taken"], errors["username"]);
        Assert.Equal(
            ["1"],
            await App.QueryAsync("SELECT count(*) FROM users"));
    }

    [Fact]
    public async Task OneAddressRegistersFiveAccountsAnHour()
    {
        using var browser = App.Browser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= RegistrationsPerHour; attempt++)
        {
            using var response = await RegisterFromAsync(browser, "203.0.113.7");
            statuses.Add(response.StatusCode);
        }

        using var limited = await RegisterFromAsync(browser, "203.0.113.7");
        using var other = await RegisterFromAsync(browser, "203.0.113.8");

        Assert.Equal(
            [.. Enumerable.Repeat(HttpStatusCode.BadRequest, RegistrationsPerHour),
                HttpStatusCode.TooManyRequests],
            statuses);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(limited, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static object Registration(
        string email = "nouveau@example.test",
        string username = "Nouveau_7") => new
        {
            email,
            username,
            password = AccountSeed.StrongPassword,
            acceptTerms = true,
        };

    // Refused by its rules, which does not spare the limit: each attempt counts.
    private static async Task<HttpResponseMessage> RegisterFromAsync(
        BrowserClient browser,
        string address)
    {
        using var request = browser.Post(RegisterPath, Registration(email: "not-an-email"));
        request.Headers.Add(ForwardedFor, address);
        return await browser.SendAsync(request);
    }
}
