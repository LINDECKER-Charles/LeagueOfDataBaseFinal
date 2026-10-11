namespace LoDb.Api.Modules.Accounts.Tokens;

/// <summary>
/// Settings of the refresh tokens of the apps (<c>LoDb:Accounts:RefreshTokens</c>).
/// </summary>
internal sealed class RefreshTokenOptions
{
    public const string SectionName = "LoDb:Accounts:RefreshTokens";

    private static readonly TimeSpan MaxReuseGrace = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long the token a refresh just replaced may still be traded, for the same new
    /// generation: an app whose answer got lost on the way, or two requests of an app that
    /// refreshed at once, keep their session. Past it, the old token revokes its family.
    /// </summary>
    public TimeSpan ReuseGrace { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasUsableGrace() => ReuseGrace >= TimeSpan.Zero && ReuseGrace <= MaxReuseGrace;
}
