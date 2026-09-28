namespace LoDb.Api.Modules.Accounts;

/// <summary>
/// The Google clients (<c>LoDb:Accounts:Google</c>): the web client, without which Google
/// sign-in is off, and the clients of the apps whose codes the exchange accepts.
/// </summary>
internal sealed class GoogleAccountOptions
{
    /// <summary>Key of the web client id, read when the services are registered.</summary>
    public const string ClientIdKey = AccountsOptions.SectionName + ":Google:" + nameof(ClientId);

    /// <summary>Id of the web client; unset, Google sign-in is off.</summary>
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    /// Clients of the apps, which get a code through the system browser and PKCE, then
    /// exchange it here (desktop loopback, Android App Link).
    /// </summary>
    public IReadOnlyList<GoogleAppClient> AppClients { get; set; } = [];

    public bool IsEnabled => !string.IsNullOrEmpty(ClientId);
}
