using System.Globalization;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The confirmation and reset e-mails: one layout (<c>account.html</c>, <c>account.txt</c>)
/// filled with the texts of <c>email.confirm.*</c> or <c>email.reset.*</c>.
/// </summary>
internal static class AccountEmailView
{
    // The lifetime of the Identity tokens (ADR 0009), when the model does not give one.
    private const int DefaultLifetimeMinutes = 60;

    public static RenderedEmail Render(
        EmailTemplate template,
        LocalizedTexts texts,
        EmailModel model)
    {
        var prefix = template == EmailTemplate.ConfirmEmail ? "email.confirm" : "email.reset";
        var userName = template == EmailTemplate.ConfirmEmail
            ? model.Required(EmailModelKeys.UserName)
            : model.Optional(EmailModelKeys.UserName);
        var values = Values(prefix, texts, model);
        values["heading"] = texts.Get($"{prefix}.heading", "name", userName ?? string.Empty);
        return new RenderedEmail
        {
            Subject = values["title"],
            Html = TemplateEngine.Render(EmailResources.Template("account.html"), values, true),
            Text = TemplateEngine.Render(EmailResources.Template("account.txt"), values, false),
            RecipientName = userName,
        };
    }

    private static Dictionary<string, string> Values(
        string prefix,
        LocalizedTexts texts,
        EmailModel model) =>
        new(StringComparer.Ordinal)
        {
            ["lang"] = texts.Language,
            ["title"] = texts.Get($"{prefix}.subject"),
            ["preheader"] = texts.Get($"{prefix}.preheader"),
            ["wordmarkSubtitle"] = texts.Get("email.wordmark_subtitle"),
            ["lede"] = texts.Get($"{prefix}.lede"),
            ["actionUrl"] = model.Required(EmailModelKeys.ActionUrl),
            ["ctaLabel"] = texts.Get($"{prefix}.cta"),
            ["fallback"] = texts.Get("email.fallback"),
            ["note"] = texts.Get($"{prefix}.expiry", "duration", texts.Duration(Lifetime(model))),
            ["footerReason"] = texts.Get($"{prefix}.footer_reason"),
            ["rights"] = texts.Get("email.footer.rights"),
        };

    private static int Lifetime(EmailModel model)
    {
        var value = model.Optional(EmailModelKeys.ExpiresInMinutes);
        if (value is null)
        {
            return DefaultLifetimeMinutes;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && minutes > 0
                ? minutes
                : throw new EmailModelException(
                    $"{EmailModelKeys.ExpiresInMinutes} is not a positive number of minutes.");
    }
}
