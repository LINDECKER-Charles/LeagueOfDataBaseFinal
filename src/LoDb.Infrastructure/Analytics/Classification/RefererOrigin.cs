namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>The referring host of a view, if any, and the source it is counted under.</summary>
/// <param name="Host">Bare host of the referrer, lowercase; null without one.</param>
/// <param name="Source">One of <see cref="RefererSources"/>.</param>
internal sealed record RefererOrigin(string? Host, string Source)
{
    public static RefererOrigin Direct { get; } = new(null, RefererSources.Direct);
}
