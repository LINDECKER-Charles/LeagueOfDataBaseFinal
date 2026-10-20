namespace LoDb.Infrastructure.Audit;

/// <summary>
/// What an audited action applies to, stored as <c>user</c>, <c>build</c>, <c>apikey</c>,
/// <c>api_client</c> or <c>prerender_pass</c>.
/// </summary>
public enum AuditTargetType
{
    User,

    Build,

    ApiKey,

    ApiClient,

    /// <summary>A pass of the pre-rendering of the catalogue, by its id.</summary>
    PrerenderPass,
}
