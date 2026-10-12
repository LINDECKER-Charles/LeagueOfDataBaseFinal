using System.Globalization;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// One host for the tests of a class, a class fixture: each test creates data of its own,
/// marked by <see cref="NextNumber"/>, sends what an address limits from
/// <see cref="NextAddress"/>, and reads back only what is its own.
/// </summary>
/// <remarks>
/// The tests of a class run one after the other: a double the host holds is the test's
/// while it runs. A test that reaches a limit, advances the clock or reads a meter starts a
/// host of its own.
/// </remarks>
/// <typeparam name="TApp">The host and its doubles.</typeparam>
public abstract class SharedApp<TApp>(PostgresContainerFixture postgres) : IAsyncLifetime
    where TApp : class, IAsyncDisposable
{
    private const string AddressPrefix = "198.18.";
    private const int AddressesPerBlock = 250;

    private TApp? _app;
    private int _numbered;

    public TApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    /// <summary>A number no other call on this host gave: the mark of one test's data.</summary>
    public int NextNumber() => Interlocked.Increment(ref _numbered);

    /// <summary>
    /// A client address no other test of the host sends from: a limit by address, such as
    /// the registrations' or the donations', counts each test apart.
    /// </summary>
    public string NextAddress()
    {
        var number = NextNumber();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{AddressPrefix}{number / AddressesPerBlock}.{(number % AddressesPerBlock) + 1}");
    }

    public async ValueTask InitializeAsync() => _app = await StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Starts the host, its database created on <paramref name="server"/>.</summary>
    protected abstract Task<TApp> StartAsync(PostgresContainerFixture server);
}
