using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>Body of <c>POST /api/account/forgot-password</c>.</summary>
internal sealed record ForgotPasswordRequest
{
    /// <summary>The e-mail of the account, whatever the case.</summary>
    public required string? Email { get; init; }

    /// <summary>Locale of the e-mail; the fallback locale when unset.</summary>
    public UiLocale? Locale { get; init; }
}
