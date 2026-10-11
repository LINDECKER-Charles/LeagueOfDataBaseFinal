using System.Collections.Frozen;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Hosting.SecurityReports.Reports;

/// <summary>
/// Reads a report body whole into memory, within its bound, then its violations.
/// </summary>
internal static class CspReportBody
{
    // report-to sends application/reports+json; report-uri application/csp-report, which
    // some browsers wrote as plain JSON.
    private static readonly FrozenSet<string> MediaTypes = new[]
    {
        "application/reports+json",
        "application/csp-report",
        "application/json",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static async Task<CspReportRead> ReadAsync(
        HttpRequest request,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsReport(request.ContentType))
        {
            return CspReportRead.Refused(CspReportRefusal.UnsupportedType);
        }

        if (request.ContentLength > maxBytes)
        {
            return CspReportRead.Refused(CspReportRefusal.Oversized);
        }

        var body = await ReadBoundedAsync(request.Body, maxBytes, cancellationToken);
        if (body is null)
        {
            return CspReportRead.Refused(CspReportRefusal.Oversized);
        }

        return CspReportFormats.Parse(body.Value) is { } violations
            ? CspReportRead.Of(violations)
            : CspReportRead.Refused(CspReportRefusal.Unreadable);
    }

    private static bool IsReport(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var parsed)
        && MediaTypes.Contains(parsed.MediaType.Value ?? string.Empty);

    // One byte past the bound tells a body that does not fit, whatever its declared length.
    private static async Task<ReadOnlyMemory<byte>?> ReadBoundedAsync(
        Stream body,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return length > maxBytes ? null : buffer.AsMemory(0, length);
    }
}
