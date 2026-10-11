using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Persistence.Audience;

/// <summary>
/// A row of <c>audience_entity_daily</c>: the page views and the visitors Umami counted on the
/// detail pages of one entity during one UTC day, pulled for <c>/v1/trends</c>.
/// </summary>
/// <remarks>
/// <see cref="Entity"/> is keyed as the <c>entities</c> of <c>analytics_daily</c>,
/// <c>{type}:{key}</c> (<c>champion:Ahri</c>, <c>item:1036</c>), so that the trends read both
/// tables alike. The figures are Umami's, as pulled: the trends bound them when they read them.
/// </remarks>
public sealed class AudienceEntityDay
{
    /// <summary>The longest key: a type, its separator and an entity key.</summary>
    public const int EntityMaxLength =
        AnalyticsEvent.TypeMaxLength + SeparatorLength + AnalyticsEvent.EntityMaxLength;

    private const int SeparatorLength = 1;

    public DateOnly Day { get; set; }

    public required string Entity { get; set; }

    public long Views { get; set; }

    public long Visitors { get; set; }
}
