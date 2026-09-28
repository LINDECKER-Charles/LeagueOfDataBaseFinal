namespace LoDb.Api.Modules.Audit.Reading.Views;

/// <summary>The activity of one account: the account, if it still exists, and a page.</summary>
internal sealed record UserActivity
{
    /// <summary>The account as stored now; null once deleted, its trail staying.</summary>
    public AuditSubjectView? Subject { get; init; }

    public required AuditPage Activity { get; init; }
}
