using LoDb.Testing;

namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// The API of the metrics endpoint tests, shared by them: no test changes its state, and its
/// revision is known, for the build information gauge.
/// </summary>
/// <remarks>
/// One test only reads the scrape: the exporter keeps its answer a few hundred milliseconds,
/// so a test that must see its own requests in a scrape needs its own host
/// (TelemetryMetricsTests).
/// </remarks>
public sealed class RevisedApi : IAsyncDisposable
{
    public const string Revision = "0123abcd";

    public ApiFactory Factory { get; } = new()
    {
        Settings = new Dictionary<string, string?> { ["APP_REVISION"] = Revision },
    };

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
