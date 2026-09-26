using System.Net.Sockets;
using LoDb.Api.Modules.PublicApi.Http;
using Npgsql;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// An outage of the database or of the storage answers go-api's <c>503 internal</c>, a fault
/// of the code its <c>500 internal</c>, however deep the cause.
/// </summary>
public sealed class DependencyFailuresTests
{
    [Fact]
    public void AnOutageOfADependencyIsTold()
    {
        Exception[] outages =
        [
            new NpgsqlException("The database is down."),
            new TimeoutException(),
            new IOException(),
            new DirectoryNotFoundException(),
            new SocketException(),
            new UnauthorizedAccessException(),
            new DependencyUnavailableException("The catalog is gone."),
            new InvalidOperationException("Wrapped.", new NpgsqlException("Down.")),
            new AggregateException(new InvalidOperationException(), new TimeoutException()),
        ];

        Assert.All(
            outages,
            static exception => Assert.True(DependencyFailures.IsOutage(exception)));
    }

    [Fact]
    public void AnyOtherFailureIsAFault()
    {
        Exception[] faults =
        [
            new InvalidOperationException(),
            new FormatException(),
            new KeyNotFoundException(),
            new InvalidOperationException("Wrapped.", new ArgumentException()),
            new AggregateException(new InvalidOperationException()),
        ];

        Assert.All(
            faults,
            static exception => Assert.False(DependencyFailures.IsOutage(exception)));
    }
}
