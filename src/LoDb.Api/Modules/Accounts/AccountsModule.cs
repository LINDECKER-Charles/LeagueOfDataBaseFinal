namespace LoDb.Api.Modules.Accounts;

/// <summary>
/// Accounts module: registration, sign-in, tokens and Google sign-in.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class AccountsModule
{
    public static IServiceCollection AddAccounts(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
