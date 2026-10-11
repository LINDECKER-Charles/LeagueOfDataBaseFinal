namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// A template with messages due now, as <c>email_outbox</c> stores it, and how many of its
/// e-mails the relay accepted within <see cref="OutboxHourlyCaps.Window"/>.
/// </summary>
/// <param name="Template">The stored template, such as <c>confirm_email</c>.</param>
/// <param name="Due">Its pending messages whose attempt is due.</param>
/// <param name="Sent">Its messages sent within the window.</param>
internal sealed record TemplateLoad(string Template, long Due, long Sent);
