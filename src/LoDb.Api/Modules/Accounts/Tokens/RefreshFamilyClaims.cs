using System.Globalization;
using System.Security.Claims;

namespace LoDb.Api.Modules.Accounts.Tokens;

/// <summary>
/// The claims that carry a <see cref="RefreshLink"/> in the tokens of an app: the bearer
/// handler writes the principal of a sign-in into both its access and its refresh token.
/// </summary>
/// <remarks>
/// Only the API reads them back, from tokens it protected itself: an app cannot set them.
/// </remarks>
internal static class RefreshFamilyClaims
{
    public const string Family = "lodb:refresh_family";
    public const string Generation = "lodb:refresh_generation";

    public static IEnumerable<Claim> Of(RefreshLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        yield return new Claim(Family, link.FamilyId.ToString("D", CultureInfo.InvariantCulture));
        yield return new Claim(
            Generation,
            link.Generation.ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer32);
    }

    /// <summary>Adds the claims of <paramref name="link"/> to the principal's identity.</summary>
    public static void AddTo(ClaimsPrincipal principal, RefreshLink link)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaims(Of(link));
        }
    }

    /// <summary>The link a principal carries; null for one signed in before the families.</summary>
    public static RefreshLink? Read(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var isFamily = Guid.TryParse(principal.FindFirstValue(Family), out var family);
        var isGeneration = int.TryParse(
            principal.FindFirstValue(Generation),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var generation);
        return isFamily && isGeneration ? new RefreshLink(family, generation) : null;
    }
}
