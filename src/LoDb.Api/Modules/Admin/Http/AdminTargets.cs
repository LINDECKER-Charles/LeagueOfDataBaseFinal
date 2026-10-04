using System.Globalization;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Builds;
using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.Admin.Http;

/// <summary>
/// The targets of the audited admin actions, named as the legacy admin named them: an
/// account by its username, a build by its name, a key by its public prefix only.
/// </summary>
internal static class AdminTargets
{
    // The prefix of a key is shown truncated, never its owner.
    private const string Ellipsis = "…";

    public static AuditTarget Of(User user) =>
        new(AuditTargetType.User, Id(user.Id), user.UserName);

    public static AuditTarget Of(Build build) =>
        new(AuditTargetType.Build, Id(build.Id), build.Name);

    public static AuditTarget Of(ApiKey key) =>
        new(AuditTargetType.ApiClient, Id(key.Id), key.KeyPrefix + Ellipsis);

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
