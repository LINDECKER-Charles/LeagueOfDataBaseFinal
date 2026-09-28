using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Console;

namespace LoDb.Api.Hosting.Logging;

/// <summary>
/// Console logging as one JSON object per line on stdout, the only thing Vector collects.
/// </summary>
internal static class LoggingRegistration
{
    public static ILoggingBuilder AddLoDbJsonConsole(this ILoggingBuilder logging)
    {
        logging.ClearProviders();

        // The trace and span ids travel as a logging scope; the formatter lifts them into
        // every line, which correlates the API with nginx, the SSR server and the database.
        logging.Configure(static options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
        logging.AddConsole(static options =>
            options.FormatterName = JsonLineConsoleFormatter.FormatterName);
        logging.AddConsoleFormatter<JsonLineConsoleFormatter, ConsoleFormatterOptions>();
        logging.Services.TryAddSingleton(TimeProvider.System);
        return logging;
    }
}
