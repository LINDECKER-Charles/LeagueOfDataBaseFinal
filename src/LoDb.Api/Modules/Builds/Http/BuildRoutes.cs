using System.Globalization;
using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Builds.Http;

/// <summary>Paths and OpenAPI tag of the builds API.</summary>
internal static class BuildRoutes
{
    /// <summary>The builds of the signed-in account, and every change it makes to them.</summary>
    public const string Builds = ApiPaths.App + "/builds";

    /// <summary>A build by the token of its <c>/b/{token}</c> link.</summary>
    public const string Share = ApiPaths.App + "/share";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "Builds";

    /// <summary>Where a build is read back for editing: the <c>Location</c> of a new one.</summary>
    public static string Of(int id) => Builds + "/" + id.ToString(CultureInfo.InvariantCulture);
}
