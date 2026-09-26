using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Pipeline;

/// <summary>
/// Registrations of the ingestion zone: patch watch, version ingestion and on-demand queue.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class IngestionRegistration
{
    public static IServiceCollection AddLoDbIngestion(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
