using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>
/// Reads the body nginx posts to the journal (docker/nginx/njs/robot-journal.js): one JSON
/// object a line, the variables of the request under short names.
/// </summary>
/// <remarks>
/// <c>{"t":1729780000.123,"ip":"203.0.113.7","m":"GET","u":"/fr/items","s":200,
/// "d":0.012,"b":5120,"a":"…","c":"HIT"}</c>: <c>t</c> nginx's <c>$msec</c>, <c>d</c> its
/// <c>$request_time</c>, in seconds. A line that is not such an object is skipped and counted.
/// </remarks>
public static class JournalLines
{
    private const byte LineEnd = (byte)'\n';
    private const char PathStart = '/';
    private const int MinStatus = 100;
    private const int MaxStatus = 599;
    private const double MillisecondsPerSecond = 1000;

    // The last second DateTimeOffset holds: 9999-12-31T23:59:59Z.
    private const double MaxUnixSeconds = 253_402_300_799;

    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <returns>The records read, and the count of lines that could not be.</returns>
    public static (IReadOnlyList<JournalRecord> Records, int Unreadable) Parse(
        ReadOnlySpan<byte> body)
    {
        var records = new List<JournalRecord>();
        var unreadable = 0;
        while (!body.IsEmpty)
        {
            var end = body.IndexOf(LineEnd);
            var line = end < 0 ? body : body[..end];
            body = end < 0 ? [] : body[(end + 1)..];
            if (line.Trim((byte)' ').IsEmpty)
            {
                continue;
            }

            if (TryRead(line, out var record))
            {
                records.Add(record);
            }
            else
            {
                unreadable++;
            }
        }

        return (records, unreadable);
    }

    /// <summary>One line of the body.</summary>
    public static bool TryRead(
        ReadOnlySpan<byte> line,
        [NotNullWhen(true)] out JournalRecord? record)
    {
        record = null;
        Line? read;
        try
        {
            read = JsonSerializer.Deserialize<Line>(line, Json);
        }
        catch (JsonException)
        {
            return false;
        }

        record = read is null ? null : RecordOf(read);
        return record is not null;
    }

    private static JournalRecord? RecordOf(Line line)
    {
        if (line.Time is not (> 0 and < MaxUnixSeconds) || string.IsNullOrEmpty(line.Method)
            || line.Target is not [PathStart, ..] || line.Status is < MinStatus or > MaxStatus)
        {
            return null;
        }

        return new JournalRecord
        {
            At = DateTimeOffset.FromUnixTimeMilliseconds(
                (long)Math.Round(line.Time.Value * MillisecondsPerSecond)),
            Address = IPAddress.TryParse(line.Address, out var address) ? address : null,
            Method = line.Method,
            Target = line.Target,
            Status = (short)line.Status,
            DurationMs = (int)Math.Clamp(
                Math.Round(line.Seconds * MillisecondsPerSecond),
                0,
                int.MaxValue),
            Bytes = Math.Max(0, line.Bytes),
            Agent = line.Agent,
            Cache = string.IsNullOrEmpty(line.Cache) ? null : line.Cache,
        };
    }

    // The names njs writes; nothing else of a line is read.
    private sealed record Line
    {
        [JsonPropertyName("t")]
        public double? Time { get; init; }

        [JsonPropertyName("ip")]
        public string? Address { get; init; }

        [JsonPropertyName("m")]
        public string? Method { get; init; }

        [JsonPropertyName("u")]
        public string? Target { get; init; }

        [JsonPropertyName("s")]
        public int Status { get; init; }

        [JsonPropertyName("d")]
        public double Seconds { get; init; }

        [JsonPropertyName("b")]
        public long Bytes { get; init; }

        [JsonPropertyName("a")]
        public string? Agent { get; init; }

        [JsonPropertyName("c")]
        public string? Cache { get; init; }
    }
}
