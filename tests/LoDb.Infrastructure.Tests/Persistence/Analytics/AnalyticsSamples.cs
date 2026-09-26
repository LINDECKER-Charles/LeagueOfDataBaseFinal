using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Tests.Persistence.Analytics;

/// <summary>Captured views with every column filled.</summary>
internal static class AnalyticsSamples
{
    public static AnalyticsEvent View(DateTimeOffset at) => new()
    {
        OccurredAt = at,
        Origin = AnalyticsCaptureOrigin.Navigation,
        Route = "champion-detail",
        Path = "/fr/champions/Ahri-ahri",
        Type = "champion",
        Kind = "detail",
        Entity = "Ahri",
        Status = 200,
        Version = "16.19.1",
        Lang = "fr_FR",
        Locale = "fr",
        Ip = "2001:db8::7",
        Visitor = new string('c', 64),
        UserAgent = "Mozilla/5.0 (X11; Linux x86_64) Firefox/140.0",
        Browser = "Firefox",
        Os = "Linux",
        Device = "desktop",
        IsBot = false,
        RefererHost = "www.google.com",
        RefererSource = "search",
        Country = "FR",
        CountryName = "France",
    };
}
