using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Contact;

/// <summary>Path and OpenAPI tag of the contact API.</summary>
internal static class ContactRoutes
{
    /// <summary>Where the form of the footer posts its messages.</summary>
    public const string Path = ApiPaths.App + "/contact";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "Contact";
}
