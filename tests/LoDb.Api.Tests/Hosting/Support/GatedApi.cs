using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting.Support;

/// <summary>
/// The API of the network gate tests, shared by them: its stack network is
/// <see cref="StackSubnet"/>.
/// </summary>
public sealed class GatedApi : IAsyncDisposable
{
    public const string StackSubnet = "10.231.1.0/24";

    public ApiFactory Factory { get; } = new()
    {
        Settings = new Dictionary<string, string?>
        {
            ["LoDb:Hosting:KnownProxyNetworks:0"] = StackSubnet,
        },
    };

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
