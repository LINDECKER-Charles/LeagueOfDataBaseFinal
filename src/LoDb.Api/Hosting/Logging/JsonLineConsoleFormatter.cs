using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace LoDb.Api.Hosting.Logging;

/// <summary>
/// Writes each record as one line of JSON: UTC timestamp, level, event name, message, trace
/// and span ids, template properties and exception.
/// </summary>
/// <remarks>
/// The built-in JSON formatter drops <see cref="EventId.Name"/>, the event key every search
/// and alert relies on, and nests the trace id in a scope array. Other scopes are left out:
/// the request scope carries the path, which may hold personal data.
/// </remarks>
internal sealed class JsonLineConsoleFormatter(TimeProvider timeProvider)
    : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "lodb-json";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const string OriginalFormatKey = "{OriginalFormat}";

    // Lines are read by people and searched by substring, never embedded in a page: accents
    // and quotes stay readable instead of being escaped.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
        {
            return;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            WriteHeader(writer, logEntry, message);
            LogTraceContext.From(scopeProvider).WriteTo(writer);
            WriteState(writer, logEntry.State);
            WriteException(writer, logEntry.Exception);
            writer.WriteEndObject();
        }

        textWriter.Write(Encoding.UTF8.GetString(buffer.WrittenSpan));
        textWriter.Write(Environment.NewLine);
    }

    private void WriteHeader<TState>(
        Utf8JsonWriter writer,
        in LogEntry<TState> logEntry,
        string? message)
    {
        var timestamp = timeProvider.GetUtcNow().UtcDateTime
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        writer.WriteString("Timestamp", timestamp);
        writer.WriteString("LogLevel", logEntry.LogLevel.ToString());
        if (!string.IsNullOrEmpty(logEntry.EventId.Name))
        {
            writer.WriteString("EventName", logEntry.EventId.Name);
        }

        writer.WriteNumber("EventId", logEntry.EventId.Id);
        writer.WriteString("Category", logEntry.Category);
        writer.WriteString("Message", message);
    }

    private static void WriteState<TState>(Utf8JsonWriter writer, TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> properties
            || !properties.Any(static property => !IsTemplate(property.Key)))
        {
            return;
        }

        writer.WriteStartObject("State");
        foreach (var (key, value) in properties.Where(static property => !IsTemplate(property.Key)))
        {
            JsonLogValue.Write(writer, key, value);
        }

        writer.WriteEndObject();
    }

    // Written only when present: the collector reads the level from the raw text, and the
    // word "Exception" alone would turn every line into an error.
    private static void WriteException(Utf8JsonWriter writer, Exception? exception)
    {
        if (exception is not null)
        {
            writer.WriteString("Exception", exception.ToString());
        }
    }

    private static bool IsTemplate(string key) =>
        string.Equals(key, OriginalFormatKey, StringComparison.Ordinal);
}
