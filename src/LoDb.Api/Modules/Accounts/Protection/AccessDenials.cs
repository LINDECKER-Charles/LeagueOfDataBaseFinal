namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// Codes of the refused requests, in the <c>code</c> of their ProblemDetails: the contract
/// the fronts switch on.
/// </summary>
internal static class AccessDenials
{
    /// <summary>401: the request needs a signed-in account.</summary>
    public const string AuthenticationRequired = "authentication-required";

    /// <summary>403: an unsafe request from a page of another origin.</summary>
    public const string OriginMismatch = "origin-mismatch";

    /// <summary>403: an unsafe request by session cookie without its XSRF token.</summary>
    public const string XsrfInvalid = "xsrf-invalid";

    /// <summary>403: the account has not verified its e-mail yet.</summary>
    public const string EmailNotVerified = "email-not-verified";

    /// <summary>403: an administrator who did not sign in with a second factor.</summary>
    public const string MfaRequired = "mfa-required";

    /// <summary>403: any other refusal.</summary>
    public const string Forbidden = "forbidden";

    // Private, so that only this class reads or writes the entry.
    private static readonly object Key = new();

    /// <summary>
    /// Keeps the forgery verdict for the problem response: a failed policy of an anonymous
    /// request ends as a bare challenge, without the reason of its failure.
    /// </summary>
    public static void Record(HttpContext context, string code) => context.Items.TryAdd(Key, code);

    public static string? Recorded(HttpContext context) =>
        context.Items.TryGetValue(Key, out var code) ? code as string : null;
}
