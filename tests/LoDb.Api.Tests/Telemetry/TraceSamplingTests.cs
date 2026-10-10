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
/// request keeps a trace id that the log lines carry; with an exporter, spans are recorded.
/// </summary>
public sealed class TraceSamplingTests
{
    private const string TraceParentHeader = "traceparent";
    private const string ParentTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string SampledParent = $"00-{ParentTraceId}-00f067aa0ba902b7-01";
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
    public async Task WithAnExporterSpansAreRecorded()
    {
        var probe = new ActivityProbe();
        using var logs = new FormattingLoggerProvider(
            new JsonLineConsoleFormatter(TimeProvider.System));
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:1",
            },
        };
        using var client = Probed(factory, probe, logs).CreateClient();

        await GetAsync(client, traceParent: null);

        Assert.True(Assert.Single(probe.Seen).IsRecorded);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Probed(
        ApiFactory factory,
        ActivityProbe probe,
        FormattingLoggerProvider logs) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(logs);
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
