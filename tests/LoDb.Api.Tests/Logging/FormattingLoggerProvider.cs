using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace LoDb.Api.Tests.Logging;

/// <summary>
/// A logger provider that runs a console formatter and keeps what it writes, record by
/// record, instead of sending it to the console.
/// </summary>
/// <remarks>
/// It receives the logger factory's scopes, including the activity scope, exactly as the
/// console provider does.
/// </remarks>
internal sealed class FormattingLoggerProvider(ConsoleFormatter formatter)
    : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<string> _records = new();
    private IExternalScopeProvider? _scopes;

    /// <summary>The text written for each record, in order.</summary>
    public IReadOnlyCollection<string> Records => _records;

    public ILogger CreateLogger(string categoryName) => new FormattingLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class FormattingLogger(FormattingLoggerProvider provider, string category)
        : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => provider._scopes?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            using var output = new StringWriter();
            var entry = new LogEntry<TState>(
                logLevel, category, eventId, state, exception, formatter);
            provider.Write(entry, output);
            provider._records.Enqueue(output.ToString());
        }
    }

    private void Write<TState>(in LogEntry<TState> entry, TextWriter output) =>
        formatter.Write(in entry, _scopes, output);
}
