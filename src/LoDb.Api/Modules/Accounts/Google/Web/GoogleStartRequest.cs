namespace LoDb.Api.Modules.Accounts.Google.Web;

/// <summary>Query of <c>GET /api/account/google/start</c>.</summary>
internal sealed record GoogleStartRequest
{
    /// <summary>
    /// The page to come back to: a path of the site, or a URL of its origin. The profile page
    /// otherwise.
    /// </summary>
    public string? ReturnUrl { get; init; }

    /// <summary>Locale of the pages to come back to, such as <c>fr</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>Keeps the session for 30 days rather than until the browser closes.</summary>
    public bool? RememberMe { get; init; }
}
