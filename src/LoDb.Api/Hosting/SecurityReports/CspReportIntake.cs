using System.Globalization;
using System.Threading.RateLimiting;
using LoDb.Api.Hosting.SecurityReports.Reports;
using LoDb.Api.Hosting.SecurityReports.Summary;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// Takes in one report body: within the bound of every client together, read within its
/// size, counted for the minute's summary and the metrics, never stored.
/// </summary>
/// <remarks>
/// The bound of every client together comes before the body is read: a flood of reports from
/// many addresses costs a refused request each, not a parse each.
/// </remarks>
internal sealed class CspReportIntake : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly string RetryAfterSeconds =
        Window.TotalSeconds.ToString(CultureInfo.InvariantCulture);

    private readonly SecurityReportsOptions _options;
    private readonly CspReportTally _tally;
    private readonly CspReportMetrics _metrics;
    private readonly FixedWindowRateLimiter _everyClient;

    public CspReportIntake(
        IOptions<SecurityReportsOptions> options,
        CspReportTally tally,
        CspReportMetrics metrics)
    {
        _options = options.Value;
        _tally = tally;
        _metrics = metrics;
        _everyClient = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = _options.PerMinute,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    public async Task<IResult> TakeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var lease = _everyClient.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            _tally.RecordShed();
            context.Response.Headers.RetryAfter = RetryAfterSeconds;
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests);
        }

        var read = await CspReportBody.ReadAsync(
            context.Request,
            _options.MaxBodyBytes,
            context.RequestAborted);
        _tally.Record(read);
        _metrics.Record(read.Violations);
        return AnswerTo(read.Refusal);
    }

    public void Dispose() => _everyClient.Dispose();

    private static IResult AnswerTo(CspReportRefusal? refusal) =>
        refusal switch
        {
            null => TypedResults.NoContent(),
            CspReportRefusal.UnsupportedType =>
                TypedResults.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType),
            CspReportRefusal.Oversized =>
                TypedResults.Problem(statusCode: StatusCodes.Status413PayloadTooLarge),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest),
        };
}
