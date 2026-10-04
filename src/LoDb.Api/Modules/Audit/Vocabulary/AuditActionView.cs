namespace LoDb.Api.Modules.Audit.Vocabulary;

/// <summary>An action of the closed set and the group the filter files it under.</summary>
/// <param name="Name">Dotted code, such as <c>user.login</c>.</param>
/// <param name="Category">auth, account, build, apikey or admin.</param>
internal sealed record AuditActionView(string Name, string Category);
