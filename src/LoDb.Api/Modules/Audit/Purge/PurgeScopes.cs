namespace LoDb.Api.Modules.Audit.Purge;

/// <summary>The scopes of an operator's purge, those of the legacy admin form.</summary>
internal static class PurgeScopes
{
    public const string Retention = "retention";
    public const string Before = "before";
    public const string All = "all";
}
