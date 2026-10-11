using LoDb.Api.Hosting;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.HttpOverrides;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// Tells the server-side renderer's calls from the others, by their connection alone: no
/// header a client could write decides it.
/// </summary>
/// <remarks>
/// The renderer calls the API directly over the stack's private network, without any
/// forwarding header. Every call through nginx carries <c>X-Forwarded-For</c>, which nginx
/// writes itself (<c>snippets/proxy-headers.conf</c>); once the forwarded headers of a
/// trusted proxy are applied, it has become <c>X-Original-For</c>. So a call is internal only
/// from the stack's own networks or the loopback (<see cref="StackNetwork"/>, the networks the
/// forwarded headers trust) and with neither header: another private network, such as one the
/// API shares with other projects, is public.
/// </remarks>
internal static class ColdCallers
{
    public static ColdCaller Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var headers = context.Request.Headers;
        if (headers.ContainsKey(ForwardedHeadersDefaults.XForwardedForHeaderName)
            || headers.ContainsKey(ForwardedHeadersDefaults.XOriginalForHeaderName))
        {
            return ColdCaller.Public;
        }

        var stack = context.RequestServices.GetRequiredService<StackNetwork>();
        return stack.Contains(context.Connection.RemoteIpAddress)
            ? ColdCaller.Internal
            : ColdCaller.Public;
    }
}
