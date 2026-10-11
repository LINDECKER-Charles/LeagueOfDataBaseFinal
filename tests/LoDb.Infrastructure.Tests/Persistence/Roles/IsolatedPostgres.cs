using LoDb.Testing;

namespace LoDb.Infrastructure.Tests.Persistence.Roles;

/// <summary>
/// A server of its own, for a test class that changes the server's roles: the assembly's
/// shared server keeps the roles every other test migrates with.
/// </summary>
public sealed class IsolatedPostgres : IAsyncLifetime
{
    public PostgresContainerFixture Server { get; } = new();

    public ValueTask InitializeAsync() => Server.InitializeAsync();

    public ValueTask DisposeAsync() => Server.DisposeAsync();
}
