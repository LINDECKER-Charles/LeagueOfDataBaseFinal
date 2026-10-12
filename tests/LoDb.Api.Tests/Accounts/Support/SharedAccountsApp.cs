using System.Globalization;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// One <see cref="AccountsApp"/> for the tests of a class, each seeding accounts named by
/// <see cref="Unique"/>.
/// </summary>
public sealed class SharedAccountsApp(PostgresContainerFixture postgres)
    : SharedApp<AccountsApp>(postgres)
{
    /// <summary>
    /// <paramref name="seed"/>, or a default account, under a username and an e-mail no
    /// other test of the host uses.
    /// </summary>
    public AccountSeed Unique(AccountSeed? seed = null)
    {
        var number = NextNumber().ToString(CultureInfo.InvariantCulture);
        return (seed ?? new AccountSeed()) with
        {
            Username = $"{AccountSeed.Name}_{number}",
            Email = $"legende{number}@example.test",
        };
    }

    protected override Task<AccountsApp> StartAsync(PostgresContainerFixture server) =>
        AccountsApp.StartSharedAsync(server);
}
