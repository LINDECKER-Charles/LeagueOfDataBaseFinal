namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>The visibility filter of the build list: public, private, or both.</summary>
internal static class BuildVisibility
{
    public const string Public = "public";
    public const string Private = "private";

    /// <summary>True for public, false for private, null for both or any other value.</summary>
    public static bool? Parse(string? visibility) => visibility switch
    {
        Public => true,
        Private => false,
        _ => null,
    };
}
