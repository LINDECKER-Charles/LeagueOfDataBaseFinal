using Microsoft.AspNetCore.Routing.Patterns;

namespace LoDb.Api.Modules.PublicApi.Gate;

/// <summary>
/// Whether a request spells its route exactly, as go-api's router requires.
/// </summary>
/// <remarks>
/// ASP.NET's routing ignores the case of the literals and a trailing slash: it would serve
/// <c>/V1/usage</c> or <c>/v1/usage/</c>, which go-api answers with its plain-text 404.
/// </remarks>
internal static class RouteShape
{
    private const char Separator = '/';

    public static bool IsExact(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEndpoint() is not RouteEndpoint endpoint)
        {
            return false;
        }

        // "/v1/usage" splits into "", "v1" and "usage".
        var segments = (context.Request.Path.Value ?? string.Empty).Split(Separator);
        var pattern = endpoint.RoutePattern.PathSegments;
        if (segments.Length != pattern.Count + 1 || segments[0].Length != 0)
        {
            return false;
        }

        for (var index = 0; index < pattern.Count; index++)
        {
            if (!Spells(pattern[index], segments[index + 1]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Spells(RoutePatternPathSegment pattern, string segment) =>
        pattern.Parts is [var part] && part switch
        {
            RoutePatternLiteralPart literal =>
                string.Equals(literal.Content, segment, StringComparison.Ordinal),
            RoutePatternParameterPart => segment.Length > 0,
            _ => false,
        };
}
