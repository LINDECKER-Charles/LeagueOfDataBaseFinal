using System.Globalization;
using System.Security.Claims;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace LoDb.Infrastructure.Audit.Journal;

/// <summary>What an audit event takes from the current request, if there is one.</summary>
internal static class AuditRequest
{
    /// <summary>
    /// The signed-in account, as an admin when it has the Admin role, or anonymous.
    /// </summary>
    public static AuditActor Actor(HttpContext? context, ClaimsIdentityOptions claims)
    {
        var principal = context?.User;
        var id = principal?.FindFirstValue(claims.UserIdClaimType);
        if (principal?.Identity?.IsAuthenticated != true
            || !int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            return AuditActor.Anonymous;
        }

        var label = principal.FindFirstValue(claims.UserNameClaimType);
        return principal.IsInRole(Role.Admin)
            ? AuditActor.Admin(userId, label)
            : AuditActor.User(userId, label);
    }

    // The forwarded headers middleware has already put the client's address here. An IPv4
    // client of a dual-stack socket is written as IPv4, as the legacy stack writes it.
    public static string? Ip(HttpContext? context)
    {
        var address = context?.Connection.RemoteIpAddress;
        return address is { IsIPv4MappedToIPv6: true }
            ? address.MapToIPv4().ToString()
            : address?.ToString();
    }

    // The pattern, not the path: the path may carry personal data.
    public static string? Route(HttpContext? context) =>
        (context?.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
}
