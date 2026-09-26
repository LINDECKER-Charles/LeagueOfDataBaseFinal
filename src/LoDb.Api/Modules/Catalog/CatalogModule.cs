namespace LoDb.Api.Modules.Catalog;

/// <summary>
/// Catalog module: metadata, lists, details, search and pickers of the Data Dragon data.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class CatalogModule
{
    public static IServiceCollection AddCatalog(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
