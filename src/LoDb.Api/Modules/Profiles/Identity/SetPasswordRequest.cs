namespace LoDb.Api.Modules.Profiles.Identity;

/// <summary>Body of <c>PUT /api/profile/password</c>.</summary>
internal sealed record SetPasswordRequest
{
    /// <summary>
    /// The first password of an account created through Google, under the rules of a new
    /// account; the form checks its confirmation.
    /// </summary>
    public required string? Password { get; init; }
}
