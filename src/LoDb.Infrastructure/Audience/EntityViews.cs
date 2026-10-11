namespace LoDb.Infrastructure.Audience;

/// <summary>
/// What Umami counted on the detail pages of one entity (<c>champion:Ahri</c>) in a day, its
/// pages in every locale added up.
/// </summary>
internal sealed record EntityViews(string Entity, long Views, long Visitors)
{
    /// <summary>The same entity with the figures of one more of its pages.</summary>
    public EntityViews Plus(long views, long visitors) =>
        this with { Views = Views + views, Visitors = Visitors + visitors };
}
