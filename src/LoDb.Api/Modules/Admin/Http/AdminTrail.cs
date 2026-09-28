namespace LoDb.Api.Modules.Admin.Http;

/// <summary>
/// The admin actions the audit vocabulary has no action for yet: the handling of contact
/// messages and the enrollment of a second factor. They go to the logs, with the acting
/// administrator, until <c>AuditAction</c> names them.
/// </summary>
/// <remarks>
/// The legacy admin did not audit the contact messages at all. The events carry ids only:
/// the log mirror of the journal leaves personal data out, and so does this trail.
/// </remarks>
internal sealed partial class AdminTrail(ILogger<AdminTrail> logger)
{
    public void ContactHandled(int adminId, int messageId) =>
        LogContactHandled(logger, adminId, messageId);

    public void ContactReopened(int adminId, int messageId) =>
        LogContactReopened(logger, adminId, messageId);

    public void ContactDeleted(int adminId, int messageId) =>
        LogContactDeleted(logger, adminId, messageId);

    public void MfaEnrolled(int adminId) => LogMfaEnrolled(logger, adminId);

    [LoggerMessage(
        EventName = "admin.contact_handle",
        Level = LogLevel.Information,
        Message = "Administrator {AdminId} marked the contact message {MessageId} handled.")]
    private static partial void LogContactHandled(ILogger logger, int adminId, int messageId);

    [LoggerMessage(
        EventName = "admin.contact_reopen",
        Level = LogLevel.Information,
        Message = "Administrator {AdminId} reopened the contact message {MessageId}.")]
    private static partial void LogContactReopened(ILogger logger, int adminId, int messageId);

    [LoggerMessage(
        EventName = "admin.contact_delete",
        Level = LogLevel.Information,
        Message = "Administrator {AdminId} deleted the contact message {MessageId}.")]
    private static partial void LogContactDeleted(ILogger logger, int adminId, int messageId);

    [LoggerMessage(
        EventName = "admin.mfa_enroll",
        Level = LogLevel.Information,
        Message = "Administrator {AdminId} enrolled an authenticator.")]
    private static partial void LogMfaEnrolled(ILogger logger, int adminId);
}
