using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Building;

/// <summary>
/// When the states a walk closes are dated (<c>closed_at</c>), the <c>lastmod</c> of their
/// opener's page: the moment of an extension, or, in a rebuild, the date the previous
/// generation gave the same state, else the rebuild's.
/// </summary>
/// <remarks>
/// A rebuild keeps the date of a state whose resource, key, opener, last version and
/// fingerprint are unchanged: the sitemaps then announce no page that did not change. One that
/// walks past the end of the previous generation, left behind the promotions, dates what it
/// closes there as the extensions it stands in for would have: after the rebuild's own date.
/// The queue of the closed states does not go by these dates but by the coverage it last read.
/// </remarks>
internal sealed class ClosingDates
{
    private readonly IReadOnlyDictionary<Identity, DateTimeOffset> kept;
    private readonly DateTimeOffset otherwise;
    private readonly PastEnd? pastEnd;

    private ClosingDates(
        IReadOnlyDictionary<Identity, DateTimeOffset> kept,
        DateTimeOffset otherwise,
        PastEnd? pastEnd = null)
    {
        this.kept = kept;
        this.otherwise = otherwise;
        this.pastEnd = pastEnd;
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

    /// <summary>
    /// The same dates, but <paramref name="moment"/> for a state closed at or after
    /// <paramref name="end"/>, the previous generation's last version, that it did not close.
    /// </summary>
    public ClosingDates After(PatchVersion end, DateTimeOffset moment) =>
        new(kept, otherwise, new PastEnd(end, moment));

    public DateTimeOffset Of(ClosedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (kept.TryGetValue(IdentityOf(state), out var closedAt))
        {
            return closedAt;
        }

        return pastEnd is { } past && state.Last >= past.End ? past.Moment : otherwise;
    }

    private static Identity IdentityOf(ClosedState state) => new(
        state.Resource,
        state.Key,
        state.Opener.Value,
        state.Last.Value,
        state.Fingerprint);

    private sealed record PastEnd(PatchVersion End, DateTimeOffset Moment);

    private sealed record Identity(
        ResourceType Resource,
        string Key,
        string Opener,
        string Last,
        string Fingerprint);
}
