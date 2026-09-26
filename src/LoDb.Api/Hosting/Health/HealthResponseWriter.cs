using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoDb.Api.Hosting.Health;

/// <summary>
/// Writes a <see cref="HealthReport"/> as a <see cref="HealthResponse"/>.
/// </summary>
internal static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var response = new HealthResponse
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value.Status.ToString(),
                StringComparer.Ordinal),
        };
        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }
}
