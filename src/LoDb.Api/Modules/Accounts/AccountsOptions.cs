namespace LoDb.Api.Modules.Accounts;

/// <summary>
/// Settings of the accounts (<c>LoDb:Accounts</c>), checked when the host starts.
/// </summary>
internal sealed class AccountsOptions
{
    public const string SectionName = "LoDb:Accounts";

    /// <summary>
    /// Public origin of the site, such as <c>https://leagueofdatabase.com</c>: the base of the
    /// links sent by e-mail, and an origin the forgery guard trusts.
    /// </summary>
    /// <remarks>
    /// Unset, the links take the origin of the request in Development and are not sent
    /// anywhere else: nginx accepts any <c>Host</c>, so a forged one would send a password
    /// reset link to the forger's host.
    /// </remarks>
    public string? SiteOrigin { get; set; }

    /// <summary>
    /// Whether the cookies are <c>Secure</c> and prefixed <c>__Host-</c>. Unset: everywhere
    /// but in Development, whose local stack is served over plain HTTP.
    /// </summary>
    public bool? SecureCookies { get; set; }

    public GoogleAccountOptions Google { get; set; } = new();
}
