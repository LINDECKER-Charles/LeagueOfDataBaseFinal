using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Api.Modules.Accounts.Security.Policy;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// <c>AddAccountsSecurity</c>: Identity's core over the LoDb stores, the migrating hasher and
/// the CNIL validator in place of Identity's, and five failures lock an account for fifteen
/// minutes. The sign-in manager chains onto it as the accounts module will chain it.
/// </summary>
public sealed class AccountsSecurityRegistrationTests
{
    // Never opened: the registration does no I/O.
    private const string UnusedDatabase = "Host=unused.invalid;Database=lodb";

    [Fact]
    public void HasherAndValidatorReplaceThoseOfIdentity()
    {
        using var services = Build(UnusedDatabase);

        Assert.IsType<MigratingPasswordHasher>(
            services.GetRequiredService<IPasswordHasher<User>>());
        Assert.IsType<CnilPasswordValidator>(
            Assert.Single(services.GetServices<IPasswordValidator<User>>()));
    }

    [Fact]
    public void FiveFailuresLockAnAccountForFifteenMinutes()
    {
        using var services = Build(UnusedDatabase);

        var lockout = services.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;

        Assert.Equal(
            (5, TimeSpan.FromMinutes(15), true),
            (lockout.MaxFailedAccessAttempts, lockout.DefaultLockoutTimeSpan,
                lockout.AllowedForNewUsers));
    }

    [Fact]
    public async Task ManagersResolveFromARequestScope()
    {
        await using var services = Build(UnusedDatabase);
        await using var scope = services.CreateAsyncScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SignInManager<User>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleManager<Role>>());
    }

    /// <summary>
    /// The services of the API around accounts: persistence, authentication and Identity with
    /// its sign-in manager, validated as the host validates them.
    /// </summary>
    internal static ServiceProvider Build(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{LoDbDataSource.ConnectionStringName}"] = connectionString,
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(TimeProvider.System)
            .AddLogging()
            .AddLoDbPersistence(configuration);
        services.AddAuthentication();
        services.AddAccountsSecurity().AddSignInManager();
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
