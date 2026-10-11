using System.Diagnostics.Metrics;
using LoDb.Api.Hosting.Telemetry;

namespace LoDb.Api.Hosting;

/// <summary>
/// <c>lodb_network_gate_refusals_total</c>: connections the <see cref="NetworkGate"/> turned
/// away, without any label (an address would be personal data and unbounded).
/// </summary>
internal sealed class NetworkGateMetrics(IMeterFactory meterFactory)
{
    public const string CounterName = "lodb.network_gate.refusals";

    private readonly Counter<long> _refusals = meterFactory
        .Create(BuildInfoMetrics.MeterName)
        .CreateCounter<long>(
            CounterName,
            unit: "{request}",
            description: "Requests to the API port from outside the stack's networks.");

    public void RecordRefusal() => _refusals.Add(1);
}
