using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Accounts.Registration;

/// <summary>Body of <c>POST /api/account/register</c>.</summary>
internal sealed record RegisterRequest
{
    /// <summary>Stored lowercase; at most 180 characters.</summary>
    public required string? Email { get; init; }

    /// <summary>
    /// 3 to 24 letters, digits, <c>_</c>, <c>.</c> or <c>-</c>, starting with a letter or a digit.
    /// </summary>
    public required string? Username { get; init; }

    /// <summary>12 characters or more, with a lowercase, an uppercase, a digit, a symbol.</summary>
    public required string? Password { get; init; }

    /// <summary>Acceptance of the terms of use, without which no account is created.</summary>
    public required bool AcceptTerms { get; init; }

    /// <summary>Locale of the verification e-mail; the fallback locale when unset.</summary>
    public UiLocale? Locale { get; init; }
}
