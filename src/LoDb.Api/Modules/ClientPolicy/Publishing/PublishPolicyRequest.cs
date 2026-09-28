namespace LoDb.Api.Modules.ClientPolicy.Publishing;

/// <summary>
/// Body of <c>PUT /api/admin/client-policy/{platform}</c>, and what <c>client-policy
/// publish</c> reads from its options: the whole policy of one app, which replaces the one
/// published before. Versions are three numbers (1.2.3).
/// </summary>
internal sealed record PublishPolicyRequest
{
    /// <summary>Below it the API answers 426; null for no floor.</summary>
    public string? MinimumVersion { get; init; }

    /// <summary>The newest release; null while none is out.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Android only: the live update bundle; null withdraws the current one.</summary>
    public BundleRequest? Bundle { get; init; }
}
