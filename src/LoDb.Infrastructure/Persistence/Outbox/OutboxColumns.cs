using LoDb.Domain.Languages;
using LoDb.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Outbox;

/// <summary>Stored spelling of the outbox values, shared by the model and the raw SQL.</summary>
internal static class OutboxColumns
{
    public static readonly ValueConverter<EmailTemplate, string> TemplateConverter = new(
        template => ToText(template),
        value => ParseTemplate(value));

    public static readonly ValueConverter<EmailOutboxStatus, string> StatusConverter = new(
        status => ToText(status),
        value => ParseStatus(value));

    public static readonly ValueConverter<UiLocale, string> LocaleConverter = new(
        locale => UiLocales.Code(locale),
        value => ParseLocale(value));

    public static string ToText(EmailTemplate template) => template switch
    {
        EmailTemplate.ConfirmEmail => "confirm_email",
        EmailTemplate.ResetPassword => "reset_password",
        EmailTemplate.ContactNotification => "contact_notification",
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null),
    };

    public static EmailTemplate ParseTemplate(string value) => value switch
    {
        "confirm_email" => EmailTemplate.ConfirmEmail,
        "reset_password" => EmailTemplate.ResetPassword,
        "contact_notification" => EmailTemplate.ContactNotification,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToText(EmailOutboxStatus status) => status switch
    {
        EmailOutboxStatus.Pending => "pending",
        EmailOutboxStatus.Sent => "sent",
        EmailOutboxStatus.Dead => "dead",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static EmailOutboxStatus ParseStatus(string value) => value switch
    {
        "pending" => EmailOutboxStatus.Pending,
        "sent" => EmailOutboxStatus.Sent,
        "dead" => EmailOutboxStatus.Dead,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static UiLocale ParseLocale(string value) =>
        UiLocales.TryParse(value, out var locale)
            ? locale
            : throw new ArgumentOutOfRangeException(nameof(value), value, null);
}
