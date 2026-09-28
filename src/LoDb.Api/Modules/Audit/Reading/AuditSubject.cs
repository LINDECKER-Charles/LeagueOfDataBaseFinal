namespace LoDb.Api.Modules.Audit.Reading;

/// <summary>The account whose activity is read, as it is stored now.</summary>
internal sealed record AuditSubject
{
    public required int Id { get; init; }

    public string? Username { get; init; }

    public string? Email { get; init; }

    public string? RiotTagline { get; init; }
}
