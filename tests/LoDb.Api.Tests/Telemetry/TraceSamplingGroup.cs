namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// The sampling of the traces, observed once no other host of the test run is alive.
/// </summary>
/// <remarks>
/// Activity sources belong to the process: every host's tracer listens to them, and the most
/// generous sampling decision wins. Another host's sampler would record the activities of the
/// host under test. In production a process runs one host.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TraceSamplingGroup
{
    public const string Name = "Trace sampling";
}
