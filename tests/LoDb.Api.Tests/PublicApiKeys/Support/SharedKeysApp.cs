using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.PublicApiKeys.Support;

/// <summary>
/// One <see cref="KeysApp"/> for the tests of a class, each with accounts of its own and
/// reading back only their keys, their journal entries and what the cache forgot of them.
/// </summary>
public sealed class SharedKeysApp(PostgresContainerFixture postgres)
    : SharedApp<KeysApp>(postgres)
{
    protected override Task<KeysApp> StartAsync(PostgresContainerFixture server) =>
        KeysApp.StartSharedAsync(server);
}
