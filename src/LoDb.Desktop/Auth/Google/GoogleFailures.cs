namespace LoDb.Desktop.Auth.Google;

/// <summary>
/// Failure codes of the Google sign-in. A refusal of the exchange by the API carries the
/// API's own code instead (<c>account-banned</c>, <c>google-email-unverified</c>…).
/// </summary>
internal static class GoogleFailures
{
    /// <summary>The build carries no Google client: same code as the API's.</summary>
    public const string Unavailable = "google-unavailable";

    public const string BrowserUnavailable = "browser-unavailable";

    /// <summary>The user declined on Google's page.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Google answered with another error, or without a code.</summary>
    public const string Denied = "google-denied";

    /// <summary>No redirect came back within the lifetime of the flow.</summary>
    public const string Expired = "expired";

    /// <summary>A redirect whose state matches no running flow; the flow goes on.</summary>
    public const string InvalidState = "invalid-state";

    /// <summary>The API refused the code without a problem code of its own.</summary>
    public const string ExchangeFailed = "google-exchange-failed";
}
