namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// The e-mails the site sends, stored as <c>confirm_email</c>, <c>reset_password</c> or
/// <c>contact_notification</c>.
/// </summary>
public enum EmailTemplate
{
    /// <summary>Link confirming the address of a new account.</summary>
    ConfirmEmail,

    /// <summary>Link setting a new password.</summary>
    ResetPassword,

    /// <summary>A contact message, forwarded to the site's team.</summary>
    ContactNotification,
}
