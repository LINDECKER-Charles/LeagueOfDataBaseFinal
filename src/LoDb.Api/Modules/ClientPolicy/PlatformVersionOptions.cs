namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>The versions of one app, as configured; unset while the app is not shipped.</summary>
internal sealed class PlatformVersionOptions
{
    /// <summary>Oldest version still served, such as 1.2.0.</summary>
    public string? MinimumVersion { get; set; }

    /// <summary>Newest version released, which an update brings.</summary>
    public string? LatestVersion { get; set; }
}
