namespace LoDb.Infrastructure.Audience;

/// <summary>
/// One pull of the audience from Umami: the views of the detail pages, per entity and per
/// UTC day, written to <c>audience_entity_daily</c> for <c>/v1/trends</c>.
/// </summary>
public interface IAudiencePull
{
    /// <summary>
    /// Rewrites the recent days (<see cref="AudienceOptions.RefreshDays"/>) with Umami's
    /// figures, and records the page views of the last closed hour.
    /// </summary>
    /// <returns>The rows written: one per entity and day.</returns>
    /// <exception cref="HttpRequestException">Umami cannot be reached in time.</exception>
    /// <exception cref="AudienceCatalogUnavailableException">
    /// The catalog cannot read the pulled pages: the day is left as it was stored.
    /// </exception>
    Task<long> PullAsync(CancellationToken cancellationToken);
}
