using LoDb.Infrastructure.Analytics.Classification;
using LoDb.Infrastructure.Analytics.Geo;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// Turns a captured view into the row of <c>analytics_event</c>, the fields of the legacy
/// <c>RequestEventFactory</c>: its page, its client (user agent, visitor hash, country) and
/// where it came from.
/// </summary>
/// <remarks>
/// An internal navigation is counted as the legacy stack counted a click on a link of the
/// site: an internal referral from the site's own host.
/// </remarks>
internal sealed class ViewEventFactory(
    ITrackedPageResolver resolver,
    VisitorHasher hasher,
    IGeoLocator geo)
{
    /// <returns>Null when the view is not of a counted page.</returns>
    public async Task<AnalyticsEvent?> CreateAsync(
        CapturedView view,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        var page = await resolver.ResolveAsync(view.Target, cancellationToken);
        return page is null ? null : Create(view, page);
    }

    private AnalyticsEvent Create(CapturedView view, TrackedPage page)
    {
        var address = view.ClientAddress is { IsIPv4MappedToIPv6: true } mapped
            ? mapped.MapToIPv4()
            : view.ClientAddress;
        var ip = address?.ToString();
        var client = UserAgentParser.Parse(view.UserAgent);
        var referer = RefererOf(view);
        var country = geo.Locate(address);
        return new AnalyticsEvent
        {
            OccurredAt = view.OccurredAt,
            Origin = view.Origin,
            Route = page.Route,
            Path = page.Path,
            Type = page.Type,
            Kind = page.Kind,
            Entity = page.Entity,
            Status = page.Status,
            Version = page.Version,
            Lang = page.Lang,
            Locale = page.Locale,
            Ip = ip,
            Visitor = hasher.Hash(ip, view.UserAgent),
            UserAgent = view.UserAgent,
            Browser = client.Browser,
            Os = client.Os,
            Device = client.Device,
            IsBot = client.IsBot,
            RefererHost = referer.Host,
            RefererSource = referer.Source,
            Country = country?.Code,
            CountryName = country?.Name,
        };
    }

    private static RefererOrigin RefererOf(CapturedView view)
    {
        if (view.Origin != AnalyticsCaptureOrigin.Navigation)
        {
            return RefererClassifier.Classify(view.Referer, view.Host);
        }

        var host = LegacyText.Lower(view.Host);
        return new RefererOrigin(host.Length == 0 ? null : host, RefererSources.Internal);
    }
}
