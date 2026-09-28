namespace LoDb.Api.Modules.Audit.Reading.Query;

/// <summary>A journal query once read: what it keeps and which page of it.</summary>
internal sealed record AuditQuery(AuditFilter Filter, PageWindow Window);
