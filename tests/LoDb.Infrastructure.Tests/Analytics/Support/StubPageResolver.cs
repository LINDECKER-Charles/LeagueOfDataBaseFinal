using LoDb.Infrastructure.Analytics.Capture;

namespace LoDb.Infrastructure.Tests.Analytics.Support;

/// <summary>
/// Resolves any address to a list page of its path, but <see cref="Skipped"/>, which is no
/// counted page, and <see cref="Failing"/>, whose resolution throws.
/// </summary>
internal sealed class StubPageResolver : ITrackedPageResolver
{
    public const string Skipped = "/skipped";
    public const string Failing = "/failing";

    public Task<TrackedPage?> ResolveAsync(string target, CancellationToken cancellationToken)
    {
        var path = target.Split('?')[0];
        return path switch
        {
            Skipped => Task.FromResult<TrackedPage?>(null),
            Failing => throw new InvalidOperationException("The catalog is out of reach."),
            _ => Task.FromResult<TrackedPage?>(new TrackedPage
            {
                Route = "app_items",
                Path = path,
                Type = "item",
                Kind = "list",
                Status = 200,
                Lang = "fr_FR",
                Locale = "fr",
            }),
        };
    }
}
