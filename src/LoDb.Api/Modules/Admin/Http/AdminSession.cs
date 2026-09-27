using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Http;

/// <summary>The administrator the current request is signed in as.</summary>
internal sealed class AdminSession(
    IHttpContextAccessor accessor,
    IOptions<IdentityOptions> identity)
{
    /// <summary>The account id of the request; 0 outside of a signed-in request.</summary>
    /// <remarks>The admin policies only let signed-in accounts through.</remarks>
    public int CurrentId
    {
        get
        {
            var type = identity.Value.ClaimsIdentity.UserIdClaimType;
            var claim = accessor.HttpContext?.User.FindFirst(type)?.Value;
            return int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id
                : 0;
        }
    }
}
