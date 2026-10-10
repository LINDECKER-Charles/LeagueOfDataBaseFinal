using System.Diagnostics.Metrics;

namespace LoDb.Api.Hosting.Errors;

/// <summary>
/// Meter <c>LoDb.Database</c>: the requests the database failed while it was out of reach,
/// scraped as <c>lodb_db_unavailable_total</c>.
/// </summary>
/// <remarks>
/// A pool that stays busy is not counted: its 503 is load shed on purpose, which the 503s of
/// the HTTP metrics and Npgsql's own <c>db.client.connection.timeouts</c> already show.
/// </remarks>
internal sealed class DatabaseMetrics
{
    public const string MeterName = "LoDb.Database";
    public const string UnavailableName = "lodb.db.unavailable";

    private readonly Counter<long> _unavailable;

    public DatabaseMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _unavailable = meterFactory.Create(MeterName).CreateCounter<long>(
            UnavailableName,
            unit: "{request}",
            description: "Requests answered 500 because the database was out of reach.");
    }

    public void RecordUnavailable() => _unavailable.Add(1);
}
