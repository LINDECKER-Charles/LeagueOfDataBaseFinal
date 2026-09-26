namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>
/// Versions of the apps (<c>LoDb:ClientPolicy</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// A configuration skeleton: the policy moves to the database, publishable without a
/// deployment, once the apps ship (ADR 0008); the endpoint's contract stays.
/// </remarks>
internal sealed class ClientPolicyOptions
{
    public const string SectionName = "LoDb:ClientPolicy";

    public PlatformVersionOptions Desktop { get; set; } = new();

    public PlatformVersionOptions Android { get; set; } = new();

    /// <summary>The versions of a platform.</summary>
    public PlatformVersionOptions Of(ClientPlatform platform) => platform switch
    {
        ClientPlatform.Desktop => Desktop,
        ClientPlatform.Android => Android,
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
    };
}
