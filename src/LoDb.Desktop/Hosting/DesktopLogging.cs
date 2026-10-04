using Microsoft.Extensions.Logging.Console;

namespace LoDb.Desktop.Hosting;

/// <summary>
/// One JSON line per record on stderr (ADR 0010): stdout carries the smoke report alone.
/// </summary>
internal static class DesktopLogging
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    // Framework categories log every request at Information: only their problems matter here.
    private static readonly string[] QuietCategories = ["Microsoft", "System.Net.Http", "Yarp"];

    public static void Configure(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(console =>
        {
            console.UseUtcTimestamp = true;
            console.TimestampFormat = TimestampFormat;
        });
        logging.Services.Configure<ConsoleLoggerOptions>(
            console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        logging.SetMinimumLevel(LogLevel.Information);
        foreach (var category in QuietCategories)
        {
            logging.AddFilter(category, LogLevel.Warning);
        }
    }
}
