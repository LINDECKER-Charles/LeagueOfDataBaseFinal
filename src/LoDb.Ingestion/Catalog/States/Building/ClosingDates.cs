using LoDb.Domain.Catalog;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// When the states a walk closes are dated (<c>closed_at</c>), the <c>lastmod</c> of their
/// opener's page: the moment of an extension, or, in a rebuild, the date the previous
/// generation gave the same state, else the rebuild's.
/// </summary>
/// <remarks>
/// A rebuild keeps the date of a state whose resource, key, opener, last version and
/// fingerprint are unchanged: the sitemaps then announce no page that did not change.
/// </remarks>
internal sealed class ClosingDates
{
    private readonly IReadOnlyDictionary<Identity, DateTimeOffset> kept;
    private readonly DateTimeOffset otherwise;

    private ClosingDates(
        IReadOnlyDictionary<Identity, DateTimeOffset> kept,
        DateTimeOffset otherwise)
    {
        this.kept = kept;
        this.otherwise = otherwise;
    }

    /// <summary>Every state closed at <paramref name="moment"/>.</summary>
    public static ClosingDates At(DateTimeOffset moment) =>
        new(new Dictionary<Identity, DateTimeOffset>(), moment);

    /// <summary>
    /// The dates of <paramref name="closed"/>, the closed states of the previous generation,
    /// for the same states; <paramref name="rebuiltAt"/> for the others.
    /// </summary>
    public static ClosingDates Keeping(
        IEnumerable<(ClosedState State, DateTimeOffset ClosedAt)> closed,
        DateTimeOffset rebuiltAt)
    {
        ArgumentNullException.ThrowIfNull(closed);
        var dates = new Dictionary<Identity, DateTimeOffset>();
        foreach (var (state, closedAt) in closed)
        {
            dates.TryAdd(IdentityOf(state), closedAt);
        }

        return new ClosingDates(dates, rebuiltAt);
    }

    public DateTimeOffset Of(ClosedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return kept.TryGetValue(IdentityOf(state), out var closedAt) ? closedAt : otherwise;
    }

    private static Identity IdentityOf(ClosedState state) => new(
        state.Resource,
        state.Key,
        state.Opener.Value,
        state.Last.Value,
        state.Fingerprint);

    private sealed record Identity(
        ResourceType Resource,
        string Key,
        string Opener,
        string Last,
        string Fingerprint);
}
