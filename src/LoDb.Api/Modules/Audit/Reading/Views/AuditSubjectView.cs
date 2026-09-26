namespace LoDb.Api.Modules.Audit.Reading.Views;

/// <summary>The account whose activity is shown.</summary>
internal sealed record AuditSubjectView
{
    public required int Id { get; init; }

    public string? Username { get; init; }

    public string? Email { get; init; }

    public static AuditSubjectView Of(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return new AuditSubjectView
        {
            Id = subject.Id,
            Username = subject.Username,
            Email = subject.Email,
        };
    }
}
