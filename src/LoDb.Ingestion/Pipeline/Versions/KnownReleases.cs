using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using Microsoft.Extensions.Caching.Hybrid;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// The versions and languages Data Dragon lists, cached ten minutes, never empty.
/// </summary>
/// <remarks>
/// A failed or empty read throws and caches nothing: a Data Dragon outage used to freeze an
/// empty version list for an hour (heritage § 4). A version missing from the cached list is
/// looked up live, once a minute at most, so a patch released since the last read is served
/// at once while made-up versions cost no more than one request a minute.
/// </remarks>
internal sealed class KnownReleases(
    HybridCache cache,
    IDdragonClient client,
    TimeProvider timeProvider)
{
    private const string VersionsKey = "lodb:ddragon:versions";
    private const string LanguagesKey = "lodb:ddragon:languages";
    private const long NeverLookedUp = 0;

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LiveLookupInterval = TimeSpan.FromMinutes(1);

    private static readonly HybridCacheEntryOptions Entry = new()
    {
        Expiration = Lifetime,
        LocalCacheExpiration = Lifetime,
    };

    private long lastLiveLookup = NeverLookedUp;

    /// <summary>Every version, newest first.</summary>
    public async Task<IReadOnlyList<PatchVersion>> GetVersionsAsync(
        CancellationToken cancellationToken)
    {
        var list = await VersionListAsync(cancellationToken).ConfigureAwait(false);
        return [.. list.Codes.Select(PatchVersion.Parse)];
    }

    public async Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
        CancellationToken cancellationToken)
    {
        var list = await cache.GetOrCreateAsync(
                LanguagesKey,
                client,
                static async (source, token) => Listed(
                    [.. (await source.GetLanguagesAsync(token).ConfigureAwait(false))
                        .Select(static language => language.Code)],
                    DdragonUrls.Languages),
                Entry,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return [.. list.Codes.Select(DdragonLanguage.Parse)];
    }

    /// <summary>
    /// Reads <c>versions.json</c> now and caches it: the patch watch's read.
    /// </summary>
    public async Task<IReadOnlyList<PatchVersion>> RefreshAsync(
        CancellationToken cancellationToken)
    {
        var versions = await client.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var list = Listed(
            [.. versions.Select(static version => version.Value)],
            DdragonUrls.Versions);
        await cache.SetAsync(VersionsKey, list, Entry, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return versions;
    }

    public async Task<bool> IsKnownAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var list = await VersionListAsync(cancellationToken).ConfigureAwait(false);
        if (list.Contains(version.Value))
        {
            return true;
        }

        return ClaimLiveLookup()
            && (await RefreshAsync(cancellationToken).ConfigureAwait(false)).Contains(version);
    }

    /// <summary>Whether Data Dragon lists both the version and the language.</summary>
    public async Task<bool> IsListedAsync(
        PatchVersion version,
        DdragonLanguage language,
        CancellationToken cancellationToken) =>
        await IsKnownAsync(version, cancellationToken).ConfigureAwait(false)
        && (await GetLanguagesAsync(cancellationToken).ConfigureAwait(false)).Contains(language);

    private static ReleaseList Listed(IReadOnlyList<string> codes, Uri source) =>
        codes.Count > 0
            ? new ReleaseList(codes)
            : throw new UpstreamDocumentException("The list is empty.", source);

    private ValueTask<ReleaseList> VersionListAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            VersionsKey,
            client,
            static async (source, token) => Listed(
                [.. (await source.GetVersionsAsync(token).ConfigureAwait(false))
                    .Select(static version => version.Value)],
                DdragonUrls.Versions),
            Entry,
            cancellationToken: cancellationToken);

    private bool ClaimLiveLookup()
    {
        var now = timeProvider.GetTimestamp();
        var last = Interlocked.Read(ref lastLiveLookup);
        if (last != NeverLookedUp
            && timeProvider.GetElapsedTime(last, now) < LiveLookupInterval)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref lastLiveLookup, now, last) == last;
    }
}
