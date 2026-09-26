using System.Net;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Analytics.Geo;

/// <summary>
/// Countries from a GeoLite2 Country database (<see cref="AnalyticsOptions.GeoIpDatabase"/>),
/// opened on the first lookup. As in the legacy stack, everything degrades to no country: no
/// database, an unreadable one, a private or unknown address.
/// </summary>
internal sealed partial class MaxMindGeoLocator(
    IOptions<AnalyticsOptions> options,
    ILogger<MaxMindGeoLocator> logger) : IGeoLocator, IDisposable
{
    private readonly Lazy<DatabaseReader?> _reader = new(
        () => Open(options.Value.GeoIpDatabase, logger),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public GeoCountry? Locate(IPAddress? address)
    {
        if (address is null || PrivateAddresses.Contains(address)
            || _reader.Value is not { } reader)
        {
            return null;
        }

        try
        {
            return reader.TryCountry(address, out var response)
                && response?.Country.IsoCode is { Length: > 0 } code
                ? new GeoCountry(code, response.Country.Name ?? code)
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A corrupt record: this view goes without a country, as any unknown address.
            return null;
        }
    }

    public void Dispose()
    {
        if (_reader.IsValueCreated)
        {
            _reader.Value?.Dispose();
        }
    }

    private static DatabaseReader? Open(string? path, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LogNoDatabase(logger);
            return null;
        }

        try
        {
            return new DatabaseReader(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or MaxMind.Db.InvalidDatabaseException)
        {
            LogUnreadable(logger, exception);
            return null;
        }
    }

    [LoggerMessage(
        EventName = "analytics.geoip.absent",
        Level = LogLevel.Information,
        Message = "No GeoLite2 database (LoDb:Analytics:GeoIpDatabase): views get no country.")]
    private static partial void LogNoDatabase(ILogger logger);

    [LoggerMessage(
        EventName = "analytics.geoip.unreadable",
        Level = LogLevel.Warning,
        Message = "The GeoLite2 database could not be opened: views get no country.")]
    private static partial void LogUnreadable(ILogger logger, Exception exception);
}
