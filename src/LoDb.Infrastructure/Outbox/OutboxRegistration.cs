using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Registrations of the outbox zone: messages persisted before being sent.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class OutboxRegistration
{
    public static IServiceCollection AddLoDbOutbox(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
