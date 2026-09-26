using Testcontainers.PostgreSql;
using Xunit;

namespace LoDb.Testing;

/// <summary>
/// A throwaway PostgreSQL server, shared by the tests of a class or a collection.
/// </summary>
/// <remarks>
/// Same major version and flavour as the production image. The container is removed when the
/// fixture is disposed, and by the Testcontainers reaper if the run dies first.
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public const string Image = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    /// <summary>Connection string of the server, for <c>ConnectionStrings:LoDb</c>.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() =>
        await _container.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
