namespace LoDb.Api.Modules.Audit.Reading;

/// <summary>The account whose activity is read, as it is stored now.</summary>
internal sealed record AuditSubject(int Id, string? Username, string? Email);
