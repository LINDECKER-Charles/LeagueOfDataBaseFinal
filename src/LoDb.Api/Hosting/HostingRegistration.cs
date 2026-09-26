using LoDb.Api.Hosting.Health;
using LoDb.Api.Hosting.Logging;
using LoDb.Api.Hosting.OpenApi;
using LoDb.Api.Hosting.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Hosting;

/// <summary>
/// Cross-cutting services and middleware of the host, called once by Program.cs.
/// </summary>
/// <remarks>
/// Everything a later chantier plugs into (authentication, authorization, antiforgery,
/// rate limiting, CORS, ProblemDetails, validation) is wired here from the start: the
/// chantiers fill their own registration files and never reorder the pipeline.
/// </remarks>
internal static class HostingRegistration
{
    /// <summary>Services shared by the web host and the command host.</summary>
    public static IHostApplicationBuilder AddLoDbCommon(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddLoDbJsonConsole();
        builder.Services.TryAddSingleton(TimeProvider.System);
        return builder;
    }

    public static WebApplicationBuilder AddLoDbHosting(this WebApplicationBuilder builder)
    {
        builder.AddLoDbCommon();
        builder.AddLoDbKestrel();
        builder.Services
            .AddLoDbTelemetry(builder.Configuration)
            .AddLoDbHealthChecks()
            .AddLoDbOpenApi()
            .AddLoDbForwardedHeaders()
            .AddLoDbCors()
            .AddLoDbRateLimiting(builder.Configuration)
            .AddLoDbAuthorization(builder.Configuration)
            .AddProblemDetails()
            .AddValidation()
            .AddAntiforgery()
            .AddAuthentication();
        return builder;
    }

    public static WebApplication UseLoDbHosting(this WebApplication app)
    {
        app.UseLoDbMetricsEndpoint();
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseWhen(
            static context => context.Request.Path.StartsWithSegments(ApiPaths.App),
            static api => api.UseStatusCodePages());
        app.UseRouting();
        app.UseLoDbCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseRateLimiter();
        return app;
    }

    public static WebApplication MapLoDbHostingEndpoints(this WebApplication app)
    {
        app.MapLoDbHealthEndpoints();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }
}
