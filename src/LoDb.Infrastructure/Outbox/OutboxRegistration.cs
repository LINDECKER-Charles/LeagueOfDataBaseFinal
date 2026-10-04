using LoDb.Infrastructure.Outbox.Delivery;
using LoDb.Infrastructure.Outbox.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Outbox;

/// <summary>
/// Registrations of the outbox zone: messages persisted before being sent.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host. The
/// zone builds on the persistence zone (the scoped context, the data source) and on the
/// host's metrics; the worker is registered by the API's convention.
/// </remarks>
public static class OutboxRegistration
{
    public static IServiceCollection AddLoDbOutbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MailOptions>()
            .Bind(configuration.GetSection(MailOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<MailOptions>, MailOptionsValidator>());
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<OutboxOptions>, OutboxOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IEmailOutbox, EmailOutbox>();
        services.TryAddSingleton<IMailTransport, SmtpMailTransport>();
        services.TryAddSingleton<EmailComposer>();
        services.TryAddSingleton<OutboxQueue>();
        services.TryAddSingleton<OutboxMetrics>();
        services.TryAddSingleton<IOutboxDispatcher, OutboxDispatcher>();
        return services;
    }
}
