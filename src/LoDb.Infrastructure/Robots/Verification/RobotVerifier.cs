using LoDb.Infrastructure.Robots.Identification;

namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>
/// The verdict on a hit and the network it came from, by the method the search engines
/// document: the address reverses to a name under the operator's domains, and that name
/// resolves back to the address.
/// </summary>
internal static class RobotVerifier
{
    /// <param name="profile">The robot the user agent names.</param>
    /// <param name="lookup">The reverse DNS of the address; null when it was put off.</param>
    public static string VerdictOf(RobotProfile profile, ReverseLookup? lookup)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsVerifiable)
        {
            return RobotVerdicts.Unverifiable;
        }

        if (lookup is null || lookup.HasFailed)
        {
            return RobotVerdicts.Unresolved;
        }

        return lookup.IsUnder(profile.VerifyDomains)
            ? RobotVerdicts.Verified
            : RobotVerdicts.Mismatch;
    }

    /// <summary>
    /// The registrable domain of a confirmed name: a name that does not resolve back is the
    /// claim of whoever holds the address, never shown as its network.
    /// </summary>
    public static string? NetworkOf(ReverseLookup? lookup) =>
        lookup is { IsConfirmed: true, Host: { } host } ? RegistrableDomain.Of(host) : null;
}
