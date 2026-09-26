namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>What one app must run, and what it may update to.</summary>
internal sealed record PlatformPolicy
{
    public required ClientPlatform Platform { get; init; }

    /// <summary>Below it the app must update before use; null while there is no floor.</summary>
    public string? MinimumVersion { get; init; }

    /// <summary>The newest release; null while none is published.</summary>
    public string? LatestVersion { get; init; }
}
