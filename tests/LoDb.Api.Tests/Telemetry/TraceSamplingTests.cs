using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using LoDb.Api.Hosting.Logging;
using LoDb.Api.Tests.Logging;
using LoDb.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// Without an exporter no span is recorded, a caller's sampled traceparent included, yet each
/// request keeps a trace id that the log lines carry; with an exporter, a share of the traces
/// is recorded, chosen by their id whatever the caller's sampled flag.
/// </summary>
[Collection(TraceSamplingGroup.Name)]
public sealed class TraceSamplingTests
{
    private const string TraceParentHeader = "traceparent";
    private const string ParentTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string ParentSpanId = "00f067aa0ba902b7";
    private const string SampledFlags = "01";
    private const string UnsampledFlags = "00";
    private const string SampledParent = $"00-{ParentTraceId}-{ParentSpanId}-{SampledFlags}";
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string UnreachableCollector = "http://127.0.0.1:1";
    private const string RatioKey = "LoDb:Telemetry:TraceSampleRatio";

    // The ratio sampler reads the first 8 bytes of the id: these sit at the two ends of it.
    private const string LowTraceId = "000000000000000a4bf92f3577b34da6";
    private const string HighTraceId = "7ffffffffffffff04bf92f3577b34da6";
    private const string HostingCategory = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private static readonly Uri Health = new("/healthz", UriKind.Relative);

    [Fact]
    public async Task WithoutExporterNothingIsRecordedAndTheLogsKeepTheTraceId()
    {
        var probe = new ActivityProbe();
        using var logs = new FormattingLoggerProvider(
            new JsonLineConsoleFormatter(TimeProvider.System));
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?>
            {
                [$"Logging:LogLevel:{HostingCategory}"] = nameof(LogLevel.Information),
            },
        };
        using var client = Probed(factory, probe, logs).CreateClient();

        await GetAsync(client, traceParent: null);
        await GetAsync(client, SampledParent);

        var seen = probe.Seen.ToList();
        Assert.Equal(2, seen.Count);
        Assert.All(seen, static activity => Assert.False(activity.IsRecorded));
        Assert.Equal(ParentTraceId, seen[1].TraceId);
        var logged = TraceIdsOf(logs, HostingCategory);
        Assert.Contains(seen[0].TraceId, logged);
        Assert.Contains(ParentTraceId, logged);
    }

    [Fact]
    public async Task WithAnExporterTheTraceIdDecidesWhateverTheCallersFlag()
    {
        var probe = new ActivityProbe();
        await using var factory = Exporting(ratio: null);
        using var client = Probed(factory, probe, logs: null).CreateClient();

        await GetAsync(client, Parent(LowTraceId, sampled: false));
        await GetAsync(client, Parent(HighTraceId, sampled: true));

        (string TraceId, bool IsRecorded)[] expected = [(LowTraceId, true), (HighTraceId, false)];
        Assert.Equal(expected, probe.Seen);
    }

    [Fact]
    public async Task TheRatioIsASetting()
    {
        var probe = new ActivityProbe();
        await using var factory = Exporting(ratio: "1");
        using var client = Probed(factory, probe, logs: null).CreateClient();

        await GetAsync(client, Parent(HighTraceId, sampled: false));

        Assert.True(Assert.Single(probe.Seen).IsRecorded);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    public async Task ARatioOutsideZeroToOneStopsTheHostAtStart(string ratio)
    {
        await using var factory = Exporting(ratio);

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("TraceSampleRatio", exception.ToString(), StringComparison.Ordinal);
    }

    private static ApiFactory Exporting(string? ratio)
    {
        var settings = new Dictionary<string, string?> { [OtlpEndpointKey] = UnreachableCollector };
        if (ratio is not null)
        {
            settings[RatioKey] = ratio;
        }

        return new ApiFactory { Settings = settings };
    }

    private static string Parent(string traceId, bool sampled) =>
        $"00-{traceId}-{ParentSpanId}-{(sampled ? SampledFlags : UnsampledFlags)}";

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Probed(
        ApiFactory factory,
        ActivityProbe probe,
        FormattingLoggerProvider? logs) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            if (logs is not null)
            {
                services.AddSingleton<ILoggerProvider>(logs);
            }

            services.AddSingleton<IStartupFilter>(probe);
        }));

    private static async Task GetAsync(HttpClient client, string? traceParent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Health);
        if (traceParent is not null)
        {
            request.Headers.Add(TraceParentHeader, traceParent);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HashSet<string> TraceIdsOf(FormattingLoggerProvider logs, string category) =>
        logs.Records
            .Select(static record => JsonSerializer.Deserialize<JsonElement>(record))
            .Where(root => root.GetProperty("Category").GetString() == category)
            .Select(static root => root.TryGetProperty("trace_id", out var id)
                ? id.GetString() ?? string.Empty
                : string.Empty)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The activity each request runs under, as the pipeline sees it.</summary>
    private sealed class ActivityProbe : IStartupFilter
    {
        public ConcurrentQueue<(string TraceId, bool IsRecorded)> Seen { get; } = new();

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    if (Activity.Current is { } activity)
                    {
                        Seen.Enqueue((activity.TraceId.ToHexString(), activity.Recorded));
                    }

                    return nextMiddleware(context);
                });
                next(app);
            };
    }
}
