using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>Paths and OpenAPI tag of the profile API.</summary>
internal static class ProfileRoutes
{
    /// <summary>The profile of the signed-in account, and every change it makes to it.</summary>
    public const string Own = ApiPaths.App + "/profile";

    /// <summary>The public cards, by username.</summary>
    public const string Public = ApiPaths.App + "/profiles";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "Profile";
}
