using LoDb.Api.Modules.Accounts.Links;
using LoDb.Domain.Languages;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The links of the account e-mails carry their token in the fragment, which the browser
/// never sends: neither the path nor the query, which the servers and the edge log, hold it.
/// </summary>
public sealed class FrontPagesTests
{
    private const string Token = "Q2ZESjhBTm9_-x";

    [Fact]
    public void VerificationLinkHoldsTheTokenInItsFragment() =>
        Assert.Equal(
            $"/fr/account/verify-email?user=7#token={Token}",
            FrontPages.VerifyEmailLink(UiLocale.Fr, 7, Token));

    [Fact]
    public void ResetLinkHoldsTheTokenInItsFragment() =>
        Assert.Equal(
            $"/en/account/reset-password?user=42#token={Token}",
            FrontPages.ResetPasswordLink(UiLocale.En, 42, Token));

    [Fact]
    public void NeitherPathNorQueryHoldsTheToken()
    {
        foreach (var link in new[]
        {
            FrontPages.VerifyEmailLink(UiLocale.En, 7, Token),
            FrontPages.ResetPasswordLink(UiLocale.En, 7, Token),
        })
        {
            var sent = new Uri(new Uri("https://example.test"), link).GetLeftPart(UriPartial.Query);
            Assert.DoesNotContain(Token, sent, StringComparison.Ordinal);
        }
    }
}
