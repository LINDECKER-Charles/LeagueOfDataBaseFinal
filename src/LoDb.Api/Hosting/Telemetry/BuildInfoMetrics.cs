using System.Diagnostics.Metrics;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// The <c>lodb_build_info{revision,version}</c> gauge, always 1.
/// </summary>
/// <remarks>
/// It ties a change seen on a dashboard to the deployment that caused it. The revision comes
/// from <c>APP_REVISION</c>, set by the image build.
/// </remarks>
internal sealed class BuildInfoMetrics(IMeterFactory meterFactory, IConfiguration configuration)
{
    public const string MeterName = "LoDb.Api";
    public const string GaugeName = "lodb.build_info";
    public const string RevisionKey = "APP_REVISION";
    public const string UnknownRevision = "unknown";
    private const string RevisionTag = "revision";
    private const string VersionTag = "version";
    private const string Description = "Revision and version of the running build, always 1.";

    // An info metric carries its data in the labels; the value only says the series exists.
    private const int InfoValue = 1;

    /// <summary>The gauge, kept with the meter the factory owns.</summary>
    public ObservableGauge<int> Gauge { get; } = Create(meterFactory, configuration);

    private static ObservableGauge<int> Create(
        IMeterFactory meterFactory,
        IConfiguration configuration)
    {
        var revision = configuration[RevisionKey];
        KeyValuePair<string, object?>[] tags =
        [
            new(RevisionTag, string.IsNullOrWhiteSpace(revision) ? UnknownRevision : revision),
            new(VersionTag, BuildVersion.Current),
        ];
        var meter = meterFactory.Create(MeterName);
        return meter.CreateObservableGauge(
            GaugeName,
            () => new Measurement<int>(InfoValue, tags),
            description: Description);
    }
}
