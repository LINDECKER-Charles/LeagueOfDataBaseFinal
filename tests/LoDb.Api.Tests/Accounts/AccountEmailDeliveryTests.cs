using System.Net;
using System.Text.RegularExpressions;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The account e-mails through the real outbox of lot L4.3: queued with the change, written
/// by its templates from the stored model, sent to a relay, and their link opens the front
/// page of L3.1 that calls the API back.
/// </summary>
public sealed partial class AccountEmailDeliveryTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string NewPassword = "N0uvelle-phrase!";
    private const int MaxBatches = 50;

    private readonly SmtpSink _relay = new();
    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RegistrationSendsTheLinkThatVerifiesTheEmail()
    {
        using var browser = App.Browser();
        using var register = await browser.PostAsync("/api/account/register", new
        {
            email = "nouveau@example.test",
            username = "Nouveau_7",
            password = AccountSeed.StrongPassword,
            acceptTerms = true,
            locale = "fr",
        });
        var id = (await ApiJson.ReadAsync(register, HttpStatusCode.Created))
            .GetProperty("user").GetProperty("id").GetInt32();

        var outcomes = await DeliverAsync();

        Assert.Equal(["sent:NULL"], outcomes);
        var mail = Assert.Single(_relay.Messages);
        Assert.Equal("nouveau@example.test", Recipient(mail));
        Assert.Contains("Nouveau_7", mail.HtmlBody, StringComparison.Ordinal);
        var link = ActionUrl(mail, "https://localhost/fr/account/verify-email?");
        var parts = EmailLink.Of(link);
        Assert.Equal(id, parts.UserId);
        using var verify = await browser.PostAsync(
            "/api/account/verify-email",
            new { userId = parts.UserId, token = parts.Token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.True((await App.FindAsync("Nouveau_7")).EmailConfirmed);
    }

    [Fact]
    public async Task ForgottenPasswordSendsTheLinkThatSetsANewOne()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();
        using var forgot = await browser.PostAsync(
            "/api/account/forgot-password",
            new { email = AccountSeed.Address });

        var outcomes = await DeliverAsync();

        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal(["sent:NULL"], outcomes);
        var mail = Assert.Single(_relay.Messages);
        Assert.Equal(AccountSeed.Address, Recipient(mail));
        var link = ActionUrl(mail, "https://localhost/en/account/reset-password/");
        var parts = EmailLink.Of(link);
        Assert.Equal(account.Id, parts.UserId);
        Assert.Equal($"?user={account.Id}", link.Query);
        using var reset = await browser.PostAsync(
            "/api/account/reset-password",
            new { userId = parts.UserId, token = parts.Token, password = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    public async ValueTask InitializeAsync() =>
        _app = await AccountsApp.StartAsync(postgres, relay: _relay);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _relay.DisposeAsync();
    }

    [GeneratedRegex(@"^https://\S+$", RegexOptions.Multiline)]
    private static partial Regex Url();

    private static string Recipient(MimeMessage mail) =>
        Assert.Single(mail.To.Mailboxes).Address;

    /// <summary>
    /// The link of the text part, which the HTML part shows escaped, and which starts with
    /// <paramref name="prefix"/>.
    /// </summary>
    private static Uri ActionUrl(MimeMessage mail, string prefix)
    {
        Assert.NotNull(mail.TextBody);
        var url = Url().Match(mail.TextBody).Value.TrimEnd('\r');
        Assert.StartsWith(prefix, url, StringComparison.Ordinal);
        Assert.Contains(
            $"href=\"{WebUtility.HtmlEncode(url)}\"",
            mail.HtmlBody,
            StringComparison.Ordinal);
        return new Uri(url);
    }

    /// <summary>
    /// Runs the dispatcher until no message is pending: the worker may take the first batch.
    /// </summary>
    /// <returns>The <c>status:last_error_code</c> of each message, oldest first.</returns>
    private async Task<IReadOnlyList<string>> DeliverAsync()
    {
        var dispatcher = App.Services.GetRequiredService<IOutboxDispatcher>();
        for (var batch = 0; batch < MaxBatches; batch++)
        {
            await dispatcher.DispatchAsync(Cancellation);
            var outcomes = await App.QueryAsync(
                "SELECT status || ':' || coalesce(last_error_code, 'NULL')"
                + " FROM email_outbox ORDER BY id");
            if (outcomes.Count > 0 && !outcomes.Any(IsPending))
            {
                return outcomes;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Cancellation);
        }

        return await App.QueryAsync("SELECT status FROM email_outbox ORDER BY id");
    }

    private static bool IsPending(string outcome) =>
        outcome.StartsWith("pending:", StringComparison.Ordinal);
}
