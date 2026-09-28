namespace LoDb.Infrastructure.Audit;

/// <summary>
/// What an audited action applies to, stored as <c>user</c>, <c>build</c>, <c>apikey</c> or
/// <c>api_client</c>.
/// </summary>
public enum AuditTargetType
{
    User,

    Build,

    ApiKey,

    ApiClient,
}
