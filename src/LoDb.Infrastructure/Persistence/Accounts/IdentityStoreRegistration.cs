using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Infrastructure.Persistence.Accounts;

/// <summary>The Identity stores over the LoDb schema.</summary>
public static class IdentityStoreRegistration
{
    /// <summary>
    /// Stores users and roles in <see cref="LoDbDbContext"/>, in place of
    /// <c>AddEntityFrameworkStores</c>, whose stores expect tables the schema does not have.
    /// </summary>
    public static IdentityBuilder AddLoDbStores(this IdentityBuilder builder)
    {
        builder.Services.TryAddScoped<IUserStore<User>, LoDbUserStore>();
        builder.Services.TryAddScoped<IRoleStore<Role>, LoDbRoleStore>();
        return builder;
    }
}
