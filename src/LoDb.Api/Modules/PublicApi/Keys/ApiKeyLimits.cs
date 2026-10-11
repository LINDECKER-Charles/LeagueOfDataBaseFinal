using LoDb.Api.Modules.Accounts.Security.Limits;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>
/// The secrets an account may have issued: each creation or regeneration writes a key,
/// revokes the previous one and empties the key cache of <c>/v1</c> for it.
/// </summary>
internal static class ApiKeyLimits
{
    private const int IssuesPerHour = 5;

    /// <summary>Creations and regenerations together, 5 an hour per account by default.</summary>
    public static AccountActionLimit Issue { get; } =
        new(Policy: "api-key-issue", Setting: "ApiKeyIssuesPerHour", IssuesPerHour);
}
