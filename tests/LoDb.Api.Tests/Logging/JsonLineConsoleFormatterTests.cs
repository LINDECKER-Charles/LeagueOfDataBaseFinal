using System.Diagnostics;
using System.Text.Json;
using LoDb.Api.Hosting.Logging;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Logging;

/// <summary>
/// Each record is one line of valid JSON carrying its event key, its level, a UTC timestamp
/// and the trace of the request that wrote it.
/// </summary>
public sealed partial class JsonLineConsoleFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 8, 30, 15, 250, TimeSpan.Zero);

    [Fact]
    public void RecordIsOneJsonLineWithItsEventNameAndTrace()
    {
        using var activity = new Activity("lodb.tests").Start();

        var line = LogOnce(logger => LogPageUnavailable(
            logger,
            "16.1.1",
            new InvalidOperationException("Upstream answered 503.\nSecond line.")));

        Assert.EndsWith(Environment.NewLine, line, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", line.TrimEnd(), StringComparison.Ordinal);
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.Equal("catalog.page.unavailable", root.GetProperty("EventName").GetString());
        Assert.Equal("Warning", root.GetProperty("LogLevel").GetString());
        Assert.Equal("2026-09-26T08:30:15.250Z", root.GetProperty("Timestamp").GetString());
        Assert.Equal(activity.TraceId.ToHexString(), root.GetProperty("trace_id").GetString());
        Assert.Equal(activity.SpanId.ToHexString(), root.GetProperty("span_id").GetString());
        Assert.Equal("16.1.1", root.GetProperty("State").GetProperty("Version").GetString());
        Assert.False(root.GetProperty("State").TryGetProperty("{OriginalFormat}", out _));
        var exception = root.GetProperty("Exception").GetString();
        Assert.Contains("Second line.", exception, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordWithoutExceptionOrActivityCarriesNeitherField()
    {
        var line = LogOnce(static logger => LogPageServed(logger, 12));

        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.Equal("catalog.page.served", root.GetProperty("EventName").GetString());

        // The collector reads the level from the raw text: the word must not appear at all.
        Assert.DoesNotContain("Exception", line, StringComparison.OrdinalIgnoreCase);
        Assert.False(root.TryGetProperty("trace_id", out _));
    }

    [Fact]
    public void NumericPropertyStaysAJsonNumber()
    {
        var line = LogOnce(static logger => LogPageServed(logger, 12));

        using var json = JsonDocument.Parse(line);
        var milliseconds = json.RootElement.GetProperty("State").GetProperty("Milliseconds");
        Assert.Equal(JsonValueKind.Number, milliseconds.ValueKind);
        Assert.Equal(12, milliseconds.GetInt32());
    }

    [Fact]
    public async Task HostWritesItsConsoleLogsWithTheJsonLineFormatter()
    {
        await using var factory = new ApiFactory();

        var console = factory.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>();
        var formatters = factory.Services.GetServices<ConsoleFormatter>();

        Assert.Equal(JsonLineConsoleFormatter.FormatterName, console.CurrentValue.FormatterName);
        Assert.Contains(formatters, static formatter => formatter is JsonLineConsoleFormatter);
    }

    private static string LogOnce(Action<ILogger> log)
    {
        using var provider = new FormattingLoggerProvider(
            new JsonLineConsoleFormatter(new FakeTimeProvider(Now)));
        using var factory = LoggerFactory.Create(logging => logging
            .Configure(static options => options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId)
            .AddProvider(provider));

        log(factory.CreateLogger("LoDb.Tests"));

        return Assert.Single(provider.Records);
    }

    [LoggerMessage(
        EventName = "catalog.page.unavailable",
        Level = LogLevel.Warning,
        Message = "Page of version {Version} unavailable.")]
    private static partial void LogPageUnavailable(
        ILogger logger,
        string version,
        Exception exception);

    [LoggerMessage(
        EventName = "catalog.page.served",
        Level = LogLevel.Information,
        Message = "Page served in {Milliseconds} ms.")]
    private static partial void LogPageServed(ILogger logger, int milliseconds);
}
