using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>Paths and OpenAPI tags of the client policy.</summary>
internal static class ClientPolicyRoutes
{
    /// <summary>The policy the apps read.</summary>
    public const string Policy = ApiPaths.App + "/client-policy";

    /// <summary>Where an administrator publishes it, one app at a time.</summary>
    public const string Admin = ApiPaths.App + "/admin/client-policy";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "ClientPolicy";

    public const string AdminTag = "AdminClientPolicy";
}
