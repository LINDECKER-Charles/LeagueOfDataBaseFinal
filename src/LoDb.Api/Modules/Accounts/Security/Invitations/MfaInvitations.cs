using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security.Invitations;

/// <summary>
/// The invitations that open the enrolment of an administrator's authenticator: printed by
/// <c>admin mfa-invite</c> on the host, each for one account and a few minutes.
/// </summary>
/// <remarks>
/// <para>
/// An administrator without a second factor signs in with a password alone: without an
/// invitation, whoever holds that password could enrol an authenticator of their own. The
/// invitation reaches only the operator who runs the command.
/// </para>
/// <para>
/// Sealed by Data Protection, whose key ring every instance and the command host share. The
/// expiry is part of the sealed payload, read against the injected clock.
/// </para>
/// </remarks>
internal sealed class MfaInvitations(
    IDataProtectionProvider protection,
    IOptions<MfaInvitationOptions> options,
    TimeProvider clock)
{
    private const string Purpose = "LoDb.Accounts.MfaInvitation";
    private const char Separator = ':';
    private const int PayloadParts = 2;

    // A sealed payload of an id and a date is a few hundred characters: anything longer
    // cannot be one, and is never decrypted.
    private const int LongestInvitation = 1024;

    private readonly IDataProtector _protector = protection.CreateProtector(Purpose);

    /// <summary>How long an invitation stays valid once issued.</summary>
    public TimeSpan Lifetime => options.Value.Lifetime;

    /// <summary>
    /// An invitation for the account <paramref name="accountId"/>, valid for
    /// <see cref="Lifetime"/>.
    /// </summary>
    public string Issue(int accountId)
    {
        var expires = clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        return _protector.Protect(
            string.Create(CultureInfo.InvariantCulture, $"{accountId}{Separator}{expires}"));
    }

    /// <summary>
    /// Whether <paramref name="invitation"/> was issued for the account
    /// <paramref name="accountId"/> and has not expired; false for anything unreadable.
    /// </summary>
    public bool Admits(int accountId, string invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        if (invitation.Length > LongestInvitation || Unsealed(invitation) is not { } payload)
        {
            return false;
        }

        var parts = payload.Split(Separator);
        return parts.Length == PayloadParts
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            && id == accountId
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            && clock.GetUtcNow().ToUnixTimeSeconds() < end;
    }

    private string? Unsealed(string invitation)
    {
        try
        {
            return _protector.Unprotect(invitation);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
