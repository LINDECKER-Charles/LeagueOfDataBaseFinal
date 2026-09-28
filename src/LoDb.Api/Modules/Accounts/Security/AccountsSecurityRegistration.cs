using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Api.Modules.Accounts.Security.Policy;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Accounts.Security;

/// <summary>Identity over <c>users</c>, with the legacy stack's hashes and policy.</summary>
internal static class AccountsSecurityRegistration
{
    /// <summary>Failed sign-ins in a row that lock an account.</summary>
    public const int MaxFailedAccessAttempts = 5;

    /// <summary>How long a locked account stays locked.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Registers Identity's core over the LoDb stores, the migrating hasher, the CNIL policy
    /// and a lockout kept in <c>users</c>, so shared by every instance.
    /// </summary>
    /// <remarks>
    /// The accounts module calls it once and chains the sign-in manager and the token
    /// providers onto the builder it returns. It does no I/O.
    /// </remarks>
    public static IdentityBuilder AddAccountsSecurity(this IServiceCollection services)
    {
        var builder = services
            .AddIdentityCore<User>(ConfigureLockout)
            .AddRoles<Role>()
            .AddLoDbStores();

        // Identity's own hasher and validator give way: the CNIL validator holds every rule.
        services.RemoveAll<IPasswordValidator<User>>();
        services.AddSingleton<IPasswordValidator<User>, CnilPasswordValidator>();
        services.Replace(
            ServiceDescriptor.Singleton<IPasswordHasher<User>, MigratingPasswordHasher>());
        services.TryAddSingleton<Argon2Passwords>();
        services.TryAddSingleton<HashingGate>();
        return builder;
    }

    // On for every account, the ones the legacy stack creates included (lockout_enabled
    // defaults to true).
    private static void ConfigureLockout(IdentityOptions options)
    {
        options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
        options.Lockout.AllowedForNewUsers = true;
    }
}
