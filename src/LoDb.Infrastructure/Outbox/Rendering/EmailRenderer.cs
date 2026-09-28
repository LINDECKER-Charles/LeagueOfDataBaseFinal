using LoDb.Domain.Languages;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// Writes a queued message in the locale stored with it, when it is sent: the request that
/// queued it plays no part (heritage I3).
/// </summary>
internal static class EmailRenderer
{
    /// <exception cref="EmailModelException">The model cannot fill the template.</exception>
    public static RenderedEmail Render(EmailTemplate template, UiLocale locale, string model)
    {
        var texts = EmailTexts.For(locale);
        var values = EmailModel.Parse(model);
        return template switch
        {
            EmailTemplate.ConfirmEmail or EmailTemplate.ResetPassword =>
                AccountEmailView.Render(template, texts, values),
            EmailTemplate.ContactNotification => ContactEmailView.Render(texts, values),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
        };
    }
}
