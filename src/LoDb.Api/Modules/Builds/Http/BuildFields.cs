namespace LoDb.Api.Modules.Builds.Http;

/// <summary>
/// Names of the request fields as the JSON writes them, the keys of <c>errors</c> in a
/// validation problem.
/// </summary>
internal static class BuildFields
{
    public const string Name = "name";
    public const string Description = "description";
    public const string Structure = "structure";
    public const string GameVersion = "gameVersion";
    public const string Language = "language";
    public const string Value = "value";
}
