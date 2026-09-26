namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Keys of <see cref="EmailMessage.Model"/> each template reads; a required key that is
/// missing or empty makes the message dead at its first attempt.
/// </summary>
/// <remarks>
/// The model holds values, never texts: the texts come from the recipient's locale when the
/// e-mail is sent, so that a queued message never depends on the request that queued it.
/// </remarks>
public static class EmailModelKeys
{
    /// <summary>
    /// Absolute link of the button, required by <see cref="EmailTemplate.ConfirmEmail"/> and
    /// <see cref="EmailTemplate.ResetPassword"/>.
    /// </summary>
    public const string ActionUrl = "actionUrl";

    /// <summary>
    /// Username of the account: the greeting of <see cref="EmailTemplate.ConfirmEmail"/>
    /// (required there) and the display name of the recipient (optional).
    /// </summary>
    public const string UserName = "userName";

    /// <summary>
    /// Lifetime of the link in whole minutes (invariant digits), shown as hours when it is a
    /// multiple of 60; one hour when absent, the lifetime of the Identity tokens.
    /// </summary>
    public const string ExpiresInMinutes = "expiresInMinutes";

    /// <summary>
    /// Category code of a contact message (<c>bug</c>, <c>feedback</c>, <c>review</c>,
    /// <c>commercial</c>), required; an unknown code is shown as is.
    /// </summary>
    public const string ContactCategory = "category";

    /// <summary>Name the visitor gave, optional.</summary>
    public const string ContactName = "name";

    /// <summary>Address of the visitor, required: the Reply-To of the notification.</summary>
    public const string ContactEmail = "email";

    /// <summary>Subject the visitor gave, optional.</summary>
    public const string ContactSubject = "subject";

    /// <summary>Text of the message, required.</summary>
    public const string ContactMessage = "message";

    /// <summary>Time the message was received, ISO 8601 with its offset, required.</summary>
    public const string ContactReceivedAt = "receivedAt";

    /// <summary>Locale the visitor wrote from, optional, shown next to the time.</summary>
    public const string ContactLocale = "locale";
}
