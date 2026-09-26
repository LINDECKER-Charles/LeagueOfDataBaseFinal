namespace LoDb.Api.Modules.Contact;

/// <summary>
/// Contact module: the contact form and its messages.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class ContactModule
{
    public static IServiceCollection AddContact(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapContact(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
