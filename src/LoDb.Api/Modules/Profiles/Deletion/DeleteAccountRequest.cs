using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Profiles.Deletion;

/// <summary>
/// Body of <c>POST /api/profile/delete</c>: the confirmation the account's
/// <c>deletionConfirmation</c> asks for; the other fields are ignored.
/// </summary>
internal sealed record DeleteAccountRequest
{
    /// <summary>The account's password, for the <c>password</c> confirmation.</summary>
    public string? Password { get; init; }

    /// <summary>The phrase of <see cref="Locale"/>, for the <c>phrase</c> confirmation.</summary>
    public string? Confirmation { get; init; }

    /// <summary>
    /// The locale of the page, whose phrase is expected; the fallback locale when unset.
    /// </summary>
    public UiLocale? Locale { get; init; }
}
