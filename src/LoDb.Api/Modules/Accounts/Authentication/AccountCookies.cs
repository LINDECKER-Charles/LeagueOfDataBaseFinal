using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// How the account cookies are written: <c>Secure</c> and prefixed <c>__Host-</c>, or plain
/// for the local stack, which is served over HTTP.
/// </summary>
internal sealed class AccountCookies(
    IOptions<AccountsOptions> options,
    IHostEnvironment environment)
{
    /// <summary>Path of every account cookie: the <c>__Host-</c> prefix requires it.</summary>
    public const string RootPath = "/";

    private const string HostPrefix = "__Host-";
    private const string SessionName = "lodb.session";
    private const string AntiforgeryName = "lodb.antiforgery";

    public bool Secure { get; } = options.Value.SecureCookies ?? !environment.IsDevelopment();

    /// <summary>Name of the session cookie.</summary>
    public string Session => Named(SessionName);

    /// <summary>Name of the cookie half of the antiforgery pair.</summary>
    public string Antiforgery => Named(AntiforgeryName);

    public CookieSecurePolicy Policy =>
        Secure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

    /// <summary>Whether the cookies may be written in answer to the request.</summary>
    /// <remarks>
    /// A <c>Secure</c> cookie over plain HTTP would be dropped by the browser, and the
    /// antiforgery services throw rather than write one.
    /// </remarks>
    public bool CanWrite(HttpRequest request) => !Secure || request.IsHttps;

    private string Named(string name) => Secure ? HostPrefix + name : name;
}
