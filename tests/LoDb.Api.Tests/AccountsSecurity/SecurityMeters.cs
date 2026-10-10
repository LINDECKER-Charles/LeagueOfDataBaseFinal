using System.Diagnostics.Metrics;
using LoDb.Api.Modules.Accounts.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// A meter factory of its own and the <see cref="SecurityMetrics"/> it holds, so that a test
/// reads only what its own objects recorded.
/// </summary>
internal sealed class SecurityMeters : IDisposable
{
    private readonly ServiceProvider _services =
        new ServiceCollection().AddMetrics().BuildServiceProvider();

    public SecurityMeters() => Metrics = new SecurityMetrics(Factory);

    public IMeterFactory Factory => _services.GetRequiredService<IMeterFactory>();

    public SecurityMetrics Metrics { get; }

    /// <summary>Collects what <paramref name="instrument"/> records from now on.</summary>
    public MetricCollector<T> Collect<T>(string instrument)
        where T : struct =>
        new(Factory, SecurityMetrics.MeterName, instrument);

    public void Dispose() => _services.Dispose();
}
