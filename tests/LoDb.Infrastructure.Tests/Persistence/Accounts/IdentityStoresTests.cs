using System.Security.Claims;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Persistence.Accounts;

/// <summary>
/// Identity's managers over the LoDb stores and an account of the legacy stack: its null
/// stamp is created at first use, never an error; roles and tokens are stored; claims,
/// external logins and passkeys, which have no table, read as empty and refuse writes.
/// </summary>
public sealed class IdentityStoresTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string TokenProvider = "[AspNetUserStore]";
    private const string TokenName = "AuthenticatorKey";
    private const string TokenValue = "JBSWY3DPEHPK3PXP";

    private ServiceProvider? _services;

    private ServiceProvider Services => _services ??= BuildServices(Database.ConnectionString);

    [Fact]
    public async Task NullStampIsCreatedAtFirstUse()
    {
        await Database.ExecuteAsync(UsersTableTests.LegacyInsert, Cancellation);
        await using var scope = Services.CreateAsyncScope();
        var users = Users(scope);
        var user = await LegacyUserAsync(users);

        var stamp = await users.GetSecurityStampAsync(user);

        Assert.Matches("^[0-9A-F]{40}$", stamp);
        Assert.Equal(stamp, await users.GetSecurityStampAsync(user));
        Assert.Equal(
            [stamp],
            await Database.QueryAsync("SELECT security_stamp FROM users", Cancellation));
    }

    [Fact]
    public async Task LaterFirstUseOfTheSameAccountKeepsTheStoredStamp()
    {
        await Database.ExecuteAsync(UsersTableTests.LegacyInsert, Cancellation);
        await using var first = Services.CreateAsyncScope();
        await using var second = Services.CreateAsyncScope();
        // Both requests load the account while its stamp is still null.
        var firstUser = await LegacyUserAsync(Users(first));
        var secondUser = await LegacyUserAsync(Users(second));

        var stored = await Users(first).GetSecurityStampAsync(firstUser);
        var later = await Users(second).GetSecurityStampAsync(secondUser);

        Assert.Equal(stored, later);
        Assert.Equal(stored, secondUser.SecurityStamp);
    }

    [Fact]
    public async Task AccountOfTheLegacyStackCanBeUpdated()
    {
        await Database.ExecuteAsync(UsersTableTests.LegacyInsert, Cancellation);
        await using var scope = Services.CreateAsyncScope();
        var users = Users(scope);
        var user = await LegacyUserAsync(users);

        user.RiotTagline = "EUW";
        var result = await users.UpdateAsync(user);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Code)));
        Assert.Equal(
            ["EUW|true"],
            await Database.QueryAsync(
                "SELECT riot_tagline || '|' || (concurrency_stamp IS NOT NULL) FROM users",
                Cancellation));
    }

    [Fact]
    public async Task RolesAndTokensAreStored()
    {
        await Database.ExecuteAsync(UsersTableTests.LegacyInsert, Cancellation);
        await using var scope = Services.CreateAsyncScope();
        var users = Users(scope);
        var user = await LegacyUserAsync(users);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        await roles.CreateAsync(new Role { Name = Role.Admin });
        await users.AddToRoleAsync(user, Role.Admin);
        await users.SetAuthenticationTokenAsync(user, TokenProvider, TokenName, TokenValue);

        Assert.Equal([Role.Admin], await users.GetRolesAsync(user));
        Assert.Equal(
            TokenValue,
            await users.GetAuthenticationTokenAsync(user, TokenProvider, TokenName));
        Assert.Equal(
            [$"ADMIN|{TokenProvider}|{TokenName}"],
            await Database.QueryAsync(
                """
                SELECT concat_ws('|', r.normalized_name, t.login_provider, t.name)
                FROM identity_user_roles ur
                JOIN identity_roles r ON r.id = ur.role_id
                JOIN identity_user_tokens t ON t.user_id = ur.user_id
                """,
                Cancellation));
    }

    [Fact]
    public async Task ClaimsPrincipalOfALegacyAccountCarriesItsRolesAndStamp()
    {
        await Database.ExecuteAsync(UsersTableTests.LegacyInsert, Cancellation);
        await using var scope = Services.CreateAsyncScope();
        var users = Users(scope);
        var user = await LegacyUserAsync(users);
        await scope.ServiceProvider.GetRequiredService<RoleManager<Role>>()
            .CreateAsync(new Role { Name = Role.Admin });
        await users.AddToRoleAsync(user, Role.Admin);
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
        var claimTypes = users.Options.ClaimsIdentity;

        var principal = await factory.CreateAsync(user);

        Assert.True(principal.IsInRole(Role.Admin));
        Assert.NotNull(user.SecurityStamp);
        Assert.Equal(
            user.SecurityStamp,
            principal.FindFirstValue(claimTypes.SecurityStampClaimType));
    }

    [Fact]
    public async Task UnmappedFeaturesReadEmptyAndRefuseWrites()
    {
        await Database.ExecuteAsync(UsersTableTests.LegacyInsert, Cancellation);
        await using var scope = Services.CreateAsyncScope();
        var users = Users(scope);
        var user = await LegacyUserAsync(users);
        var login = new UserLoginInfo("Google", "109876543210987654321", "Google");

        Assert.Empty(await users.GetClaimsAsync(user));
        Assert.Empty(await users.GetLoginsAsync(user));
        Assert.Empty(await users.GetPasskeysAsync(user));
        Assert.Null(await users.FindByLoginAsync(login.LoginProvider, login.ProviderKey));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => users.AddClaimAsync(user, new Claim("plan", "pro")));
        await Assert.ThrowsAsync<NotSupportedException>(() => users.AddLoginAsync(user, login));
    }

    [Fact]
    public async Task RoleClaimsReadEmptyAndRefuseWrites()
    {
        await using var scope = Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var role = new Role { Name = Role.Admin };
        await roles.CreateAsync(role);

        Assert.Empty(await roles.GetClaimsAsync(role));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => roles.AddClaimAsync(role, new Claim("permission", "ban")));
    }

    protected override async ValueTask DisposeServicesAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
    }

    private static UserManager<User> Users(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    private static async Task<User> LegacyUserAsync(UserManager<User> users) =>
        await users.FindByEmailAsync("legende@example.test")
            ?? throw new InvalidOperationException("The legacy account is missing.");

    // Persistence as the API registers it, and Identity as the accounts module does, without
    // the hasher and validators of the API.
    private static ServiceProvider BuildServices(string connectionString)
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
        services.AddIdentityCore<User>().AddRoles<Role>().AddLoDbStores();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
