using LoDb.Api.Hosting;
using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting.Support;

/// <summary>
/// The API of the host filtering tests, shared by them: <c>AllowedHosts</c> lists its public
/// hosts, its stack alias and the loopback.
/// </summary>
public sealed class FilteredHostsApi : IAsyncDisposable
{
    public const string Hosts =
        "example.test, example.fr,api.example.test;lodb-api ,localhost,127.0.0.1";

    public ApiFactory Factory { get; } = new()
    {
        Settings = new Dictionary<string, string?> { [HostFilteringSetup.Key] = Hosts },
    };

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
