namespace LoDb.Api.Modules.Accounts;

/// <summary>A Google client of an app, whose codes the exchange accepts.</summary>
internal sealed class GoogleAppClient
{
    public string? ClientId { get; set; }

    /// <summary>
    /// Set for a desktop client, whose secret ships with the app and is no secret; unset for
    /// an Android client, which has none.
    /// </summary>
    public string? ClientSecret { get; set; }
}
