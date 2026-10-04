namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>
/// Names of the request fields as the JSON writes them, the keys of <c>errors</c> in a
/// validation problem.
/// </summary>
internal static class ProfileFields
{
    public const string Username = "username";
    public const string RiotTagline = "riotTagline";
    public const string Version = "version";
    public const string Password = "password";
    public const string Confirmation = "confirmation";
}
