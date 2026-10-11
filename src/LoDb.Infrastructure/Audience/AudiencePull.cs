using System.Net;
using LoDb.Infrastructure.Analytics.Aggregation;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Audience.Umami;
using LoDb.Infrastructure.Persistence.Audience;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Audience;

/// <summary>
/// Pulls from Umami the views of the detail pages, per entity and per UTC day, into
/// <c>audience_entity_daily</c>: the same entities the in-house measure counts, read by the
/// grammar of the site's addresses (<see cref="ITrackedPageResolver"/>).
/// </summary>
/// <remarks>
/// <para>
/// Only a detail of the latest version that the catalog holds counts, under its canonical
/// address, as the in-house measure counts it: a path Umami received but that names no such
/// entity, a list, the home page, a pinned version or a crafted address are left out. An
/// entity seen in several locales adds their views and their visitors, a visitor of two
/// locales then counting twice.
/// </para>
/// <para>
/// Each pull rewrites the last <see cref="AudienceOptions.RefreshDays"/> days whole, so a day
/// is final once a pull has run after its end.
/// </para>
/// </remarks>
internal sealed class AudiencePull(
    UmamiClient umami,
    ITrackedPageResolver pages,
    AudienceStore store,
    AudienceMetrics metrics,
    IOptions<AudienceOptions> options,
    TimeProvider timeProvider) : IAudiencePull
{
    // TrackedPage.Kind of a detail page, and the status of a page that exists.
    private const string DetailKind = "detail";
    private const short Found = (short)HttpStatusCode.OK;

    public async Task<long> PullAsync(CancellationToken cancellationToken)
    {
        var token = await umami.SignInAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        long rows = 0;
        for (var back = options.Value.RefreshDays - 1; back >= 0; back--)
        {
            var day = today.AddDays(-back);
            var paths = await umami.ReadPathsAsync(token, UmamiWindow.Of(day), cancellationToken);
            var entities = await EntitiesOfAsync(paths, cancellationToken);
            await store.ReplaceDayAsync(day, entities, cancellationToken);
            rows += entities.Count;
        }

        var lastHour = UmamiWindow.HourBefore(now);
        metrics.RecordPulledViews(
            await umami.ReadPageViewsAsync(token, lastHour, cancellationToken));
        return rows;
    }

    private async Task<IReadOnlyCollection<EntityViews>> EntitiesOfAsync(
        IReadOnlyList<UmamiPathViews> paths,
        CancellationToken cancellationToken)
    {
        var entities = new Dictionary<string, EntityViews>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (await EntityOfAsync(path.Path, cancellationToken) is not { } entity)
            {
                continue;
            }

            entities[entity] = entities.TryGetValue(entity, out var counted)
                ? counted.Plus(path.Views, path.Visitors)
                : new EntityViews(entity, path.Views, path.Visitors);
        }

        return entities.Values;
    }

    private async Task<string?> EntityOfAsync(string path, CancellationToken cancellationToken)
    {
        var page = await pages.ResolveAsync(path, cancellationToken);
        if (page is not { Kind: DetailKind, Status: Found, Entity: { } key })
        {
            return null;
        }

        var entity = page.Type + DailyBuckets.EntitySeparator + key;
        return entity.Length <= AudienceEntityDay.EntityMaxLength ? entity : null;
    }
}
