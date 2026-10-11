using LoDb.Api.Hosting;
using LoDb.Api.Tests.Hosting.Support;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// A wrong network or request setting stops the host at start, with the name of the setting,
/// instead of serving on a port nobody expects.
/// </summary>
public sealed class HostingOptionsTests
{
    [Theory]
    [InlineData("LoDb:Hosting:MetricsPort", "8080", "must differ")]
    [InlineData("LoDb:Hosting:KnownProxyNetworks:0", "not-a-network", "CIDR")]
    [InlineData("LoDb:Hosting:DatabaseBusyRetryAfter", "00:00:00", "DatabaseBusyRetryAfter")]
    public void InvalidSettingStopsTheHostAtStart(string key, string value, string reason)
    {
        var refusal = StartupValidation.RefusalOf<HostingOptions>(
            static builder => builder.AddLoDbKestrel(),
            new Dictionary<string, string?> { [key] = value });

        Assert.Contains(reason, refusal.Message, StringComparison.Ordinal);
    }
}
