using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Outbox.Rendering;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// An e-mail is written in its recipient's locale, English when that locale has no texts;
/// the contact notification keeps its legacy subject and Reply-To; values are escaped in
/// HTML only.
/// </summary>
public sealed class EmailRendererTests
{
    public static TheoryData<UiLocale> UntranslatedLocales =>
        [.. UiLocales.All.Where(static locale => locale is not (UiLocale.En or UiLocale.Fr))];

    public static TheoryData<EmailTemplate, UiLocale> EveryTemplateAndLocale
    {
        get
        {
            var data = new TheoryData<EmailTemplate, UiLocale>();
            foreach (var template in Enum.GetValues<EmailTemplate>())
            {
                foreach (var locale in UiLocales.All)
                {
                    data.Add(template, locale);
                }
            }

            return data;
        }
    }

    [Fact]
    public void StoredLocaleChoosesTheTexts()
    {
        var french = Render(EmailTemplate.ConfirmEmail, UiLocale.Fr, OutboxHarness.AccountModel());
        var english = Render(EmailTemplate.ConfirmEmail, UiLocale.En, OutboxHarness.AccountModel());

        Assert.Equal("Confirmez votre adresse e-mail · LeagueOfDataBase", french.Subject);
        Assert.Contains("<html lang=\"fr\"", french.Html, StringComparison.Ordinal);
        Assert.Contains("Bienvenue, Faker", french.Text, StringComparison.Ordinal);
        Assert.Contains("Ce lien expire dans 1 heure.", french.Text, StringComparison.Ordinal);
        Assert.Equal("Confirm your email address · LeagueOfDataBase", english.Subject);
        Assert.Contains("This link expires in 1 hour.", english.Text, StringComparison.Ordinal);
        Assert.Equal("Faker", french.RecipientName);
    }

    [Theory]
    [MemberData(nameof(UntranslatedLocales))]
    public void LocaleWithoutTextsFallsBackToEnglish(UiLocale locale)
    {
        var email = Render(EmailTemplate.ResetPassword, locale, OutboxHarness.AccountModel());

        Assert.Equal("Reset your password · LeagueOfDataBase", email.Subject);
        Assert.Contains("<html lang=\"en\"", email.Html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryTemplateAndLocale))]
    public void EveryTemplateRendersInEveryLocale(EmailTemplate template, UiLocale locale)
    {
        var model = template == EmailTemplate.ContactNotification
            ? OutboxHarness.ContactModel()
            : OutboxHarness.AccountModel();

        var email = Render(template, locale, model);

        Assert.DoesNotContain("{{", email.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", email.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("%", email.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void FrenchCatalogTranslatesEveryEnglishKey()
    {
        var english = EmailTexts.For(UiLocale.En);
        var french = EmailTexts.For(UiLocale.Fr);

        Assert.Equal([UiLocale.En, UiLocale.Fr], EmailTexts.Translated.Order());
        Assert.Equal(UiLocale.Fr, french.Locale);
        Assert.Equal(UiLocale.En, EmailTexts.For(UiLocale.De).Locale);
        Assert.NotEqual(english.Get("email.fallback"), french.Get("email.fallback"));
    }

    [Theory]
    [InlineData(null, UiLocale.En, "1 hour")]
    [InlineData("30", UiLocale.En, "30 minutes")]
    [InlineData("1", UiLocale.En, "1 minute")]
    [InlineData("120", UiLocale.Fr, "2 heures")]
    public void LinkLifetimeFollowsTheModel(string? minutes, UiLocale locale, string expected)
    {
        var model = OutboxHarness.AccountModel(minutes);

        var email = Render(EmailTemplate.ResetPassword, locale, model);

        Assert.Contains($" {expected}.", email.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Page blanche", "Visiteur", "[Contact · Bug / anomalie] Page blanche")]
    [InlineData(null, "Visiteur", "[Contact · Bug / anomalie] Visiteur")]
    [InlineData(null, null, "[Contact · Bug / anomalie] visitor@example.com")]
    public void ContactSubjectKeepsTheLegacyShape(string? subject, string? name, string expected)
    {
        var model = OutboxHarness.ContactModel(subject, name);

        var email = Render(EmailTemplate.ContactNotification, UiLocale.Fr, model);

        Assert.Equal(expected, email.Subject);
        Assert.Equal("visitor@example.com", email.ReplyTo!.Address);
        Assert.Equal(name ?? string.Empty, email.ReplyTo.Name ?? string.Empty);
        Assert.Equal(subject is not null, email.Text.Contains("Sujet :", StringComparison.Ordinal));
    }

    [Fact]
    public void ValuesAreEscapedInHtmlOnly()
    {
        var model = OutboxHarness.AccountModel(userName: "<b>Faker & co</b>");

        var email = Render(EmailTemplate.ConfirmEmail, UiLocale.En, model);

        Assert.Contains(
            "Welcome, &lt;b&gt;Faker &amp; co&lt;/b&gt;",
            email.Html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Faker", email.Html, StringComparison.Ordinal);
        Assert.Contains("Welcome, <b>Faker & co</b>", email.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[1, 2]")]
    [InlineData("not json")]
    public void UnusableModelIsAModelError(string model)
    {
        var error = Assert.Throws<EmailModelException>(() =>
            EmailRenderer.Render(EmailTemplate.ConfirmEmail, UiLocale.En, model));

        Assert.DoesNotContain("@", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownContactCategoryIsShownAsItIs()
    {
        var model = OutboxHarness.ContactModel();
        model[EmailModelKeys.ContactCategory] = "partnership";

        var email = Render(EmailTemplate.ContactNotification, UiLocale.En, model);

        Assert.StartsWith("[Contact · partnership] ", email.Subject, StringComparison.Ordinal);
    }

    private static RenderedEmail Render(
        EmailTemplate template,
        UiLocale locale,
        IReadOnlyDictionary<string, string?> model) =>
        EmailRenderer.Render(template, locale, JsonSerializer.Serialize(model));
}
