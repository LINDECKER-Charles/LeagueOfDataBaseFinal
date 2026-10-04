using System.Text.Json;

namespace LoDb.Api.Hosting.Logging;

/// <summary>
/// Trace and span ids of the current activity, read from the logging scopes.
/// </summary>
/// <remarks>
/// The logger factory puts them in the first scope when activity tracking is on. They are
/// written as <c>trace_id</c> and <c>span_id</c>, the names the SSR server uses, so that one
/// search follows a request across both.
/// </remarks>
internal sealed class LogTraceContext
{
    private const string TraceIdScopeKey = "TraceId";
    private const string SpanIdScopeKey = "SpanId";
    private const string TraceIdField = "trace_id";
    private const string SpanIdField = "span_id";

    private string? _traceId;
    private string? _spanId;

    public static LogTraceContext From(IExternalScopeProvider? scopeProvider)
    {
        var context = new LogTraceContext();
        scopeProvider?.ForEachScope(static (scope, found) => found.Read(scope), context);
        return context;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        if (_traceId is not null)
        {
            writer.WriteString(TraceIdField, _traceId);
        }

        if (_spanId is not null)
        {
            writer.WriteString(SpanIdField, _spanId);
        }
    }

    private void Read(object? scope)
    {
        if (scope is not IEnumerable<KeyValuePair<string, object?>> properties)
        {
            return;
        }

        foreach (var (key, value) in properties)
        {
            _traceId ??= IsKey(key, TraceIdScopeKey) ? value?.ToString() : null;
            _spanId ??= IsKey(key, SpanIdScopeKey) ? value?.ToString() : null;
        }
    }

    private static bool IsKey(string key, string expected) =>
        string.Equals(key, expected, StringComparison.Ordinal);
}
