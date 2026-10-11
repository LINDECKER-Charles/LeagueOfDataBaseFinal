using System.Globalization;
using LoDb.Api.Modules.Accounts.Security.Invitations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>
/// The invitation that opens the enrolment of an administrator's authenticator: it admits
/// the account it was issued for, until it expires, and nothing unreadable or too long.
/// </summary>
public sealed class MfaInvitationsTests
{
    private const int Account = 7;
    private const int OtherAccount = 8;
    private const int LongestInvitation = 1024;
    private const string Purpose = "LoDb.Accounts.MfaInvitation";

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    // A whole second: the expiry is sealed in Unix seconds.
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));

    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly MfaInvitations _invitations;

    public MfaInvitationsTests() => _invitations = new MfaInvitations(
        _protection,
        Options.Create(new MfaInvitationOptions()),
        _clock);

    [Fact]
    public void InvitationAdmitsOnlyItsAccount()
    {
        var invitation = _invitations.Issue(Account);

        Assert.True(_invitations.Admits(Account, invitation));
        Assert.False(_invitations.Admits(OtherAccount, invitation));
    }

    [Fact]
    public void InvitationExpiresAtTheEndOfItsLifetime()
    {
        var invitation = _invitations.Issue(Account);

        _clock.Advance(_invitations.Lifetime - OneSecond);
        var justBefore = _invitations.Admits(Account, invitation);
        _clock.Advance(OneSecond);

        Assert.True(justBefore);
        Assert.False(_invitations.Admits(Account, invitation));
    }

    [Theory]
    [InlineData("forged")]
    [InlineData("")]
    public void UnreadableInvitationAdmitsNoOne(string invitation) =>
        Assert.False(_invitations.Admits(Account, invitation));

    // Sealed with the invitations' key and purpose, its id padded with zeros: the longest
    // one within the bound is admitted, the first one past it is never opened.
    [Fact]
    public void InvitationLongerThanAnyIssuedIsNeverOpened()
    {
        var padding = 0;
        while (Sealed(padding).Length <= LongestInvitation)
        {
            padding++;
        }

        Assert.Equal(
            (true, false),
            (_invitations.Admits(Account, Sealed(padding - 1)),
                _invitations.Admits(Account, Sealed(padding))));
    }

    private string Sealed(int padding)
    {
        var expires = (_clock.GetUtcNow() + _invitations.Lifetime).ToUnixTimeSeconds();
        return _protection.CreateProtector(Purpose).Protect(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{new string('0', padding)}{Account}:{expires}"));
    }
}
