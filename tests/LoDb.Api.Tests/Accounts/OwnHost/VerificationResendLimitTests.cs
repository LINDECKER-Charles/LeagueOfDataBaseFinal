using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>
/// The verification e-mail asked for again: five times in all, then once a day.
/// </summary>
/// <remarks>A host of its own: the test advances its clock a day.</remarks>
public sealed class VerificationResendLimitTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string ResendPath = "/api/account/verify-email/resend";
    private const int FreeResends = 5;
    private static readonly TimeSpan ResendInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task ResendIsLimitedToFiveInAllThenOneADay()
    {
        await App.SeedAsync(new AccountSeed { EmailConfirmed = false });
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name, rememberMe: true);
        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < FreeResends; attempt++)
        {
            using var sent = await browser.PostAsync(ResendPath);
            statuses.Add(sent.StatusCode);
            App.Clock.Advance(Minute);
        }

        // The fifth went a minute ago: the sixth waits a day after it.
        using var sixth = await browser.PostAsync(ResendPath);
        App.Clock.Advance(ResendInterval - (2 * Minute));
        using var early = await browser.PostAsync(ResendPath);
        App.Clock.Advance(Minute);
        using var nextDay = await browser.PostAsync(ResendPath);
        using var sameDay = await browser.PostAsync(ResendPath);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.Accepted, status));
        Assert.Equal(
            "resend-throttled",
            await ApiJson.ProblemCodeAsync(sixth, HttpStatusCode.TooManyRequests));
        Assert.Equal(ResendInterval - Minute, sixth.Headers.RetryAfter?.Delta);
        Assert.Equal(Minute, early.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.Accepted, nextDay.StatusCode);
        Assert.Equal(ResendInterval, sameDay.Headers.RetryAfter?.Delta);
        Assert.Equal(FreeResends + 1, App.Outbox.Sent(EmailTemplate.ConfirmEmail).Count);
        Assert.All(
            App.Outbox.Messages,
            static message => Assert.Equal(UiLocale.En, message.Locale));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
