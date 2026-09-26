using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Contact;

/// <summary>
/// Contact module: the contact form and its messages.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The admin inbox of
/// the messages belongs to the admin module.
/// </remarks>
internal static class ContactModule
{
    public static IServiceCollection AddContact(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ContactOptions>()
            .Bind(configuration.GetSection(ContactOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<SendContactEndpoint>();
        return services;
    }

    public static IEndpointRouteBuilder MapContact(this IEndpointRouteBuilder endpoints)
    {
        SendContactEndpoint.Map(endpoints);
        return endpoints;
    }
}
