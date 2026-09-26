namespace LoDb.Api.Modules.Billing;

/// <summary>
/// Billing module: Stripe checkout, idempotent webhooks, credits and donations.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class BillingModule
{
    public static IServiceCollection AddBilling(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapBilling(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
