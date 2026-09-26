using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>Body of <c>POST /api/account/verify-email/resend</c>.</summary>
internal sealed record ResendVerificationRequest
{
    /// <summary>Locale of the e-mail; the fallback locale when unset.</summary>
    public UiLocale? Locale { get; init; }
}
