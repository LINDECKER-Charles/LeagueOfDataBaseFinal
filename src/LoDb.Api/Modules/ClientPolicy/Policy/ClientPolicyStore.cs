using LoDb.Api.Modules.ClientPolicy.Publishing;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Apps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace LoDb.Api.Modules.ClientPolicy.Policy;

/// <summary>
/// The policy of the apps, stored in <c>client_policy</c> (ADR 0004: mutable state lives in
/// the database) and cached for a minute.
/// </summary>
/// <remarks>
/// A publication clears this instance's cache at once; another instance of the API follows
/// within the minute, which is the delay ADR 0008 accepts to withdraw a release.
/// </remarks>
internal sealed partial class ClientPolicyStore(
    IDbContextFactory<LoDbDbContext> contexts,
    HybridCache cache,
    TimeProvider clock,
    ILogger<ClientPolicyStore> logger)
{
    private const string CacheKey = "lodb:client-policy";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private static readonly HybridCacheEntryOptions CacheEntry = new()
    {
        Expiration = Lifetime,
        LocalCacheExpiration = Lifetime,
    };

    /// <summary>The policy, at most a minute old.</summary>
    public ValueTask<AppPolicy> ReadAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKey,
            this,
            static (store, cancel) => store.LoadAsync(cancel),
            CacheEntry,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Replaces the policy of <paramref name="platform"/> with a request the rules accepted:
    /// what it leaves out is cleared, not kept.
    /// </summary>
    public async Task<PlatformPolicy> PublishAsync(
        ClientPlatform platform,
        PublishPolicyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var key = ClientPlatforms.ToRow(platform);
        var row = await db.ClientPolicies.FindAsync([key], cancellationToken);
        if (row is null)
        {
            row = new ClientPolicyEntry { Platform = key };
            db.ClientPolicies.Add(row);
        }

        Fill(row, request);
        row.PublishedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKey, cancellationToken);
        var name = ClientPlatforms.NameOf(platform);
        LogPublished(
            logger,
            name,
            row.MinimumVersion,
            row.LatestVersion,
            row.BundleId);
        return PlatformPolicy.Of(platform, row);
    }

    private async ValueTask<AppPolicy> LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.ClientPolicies.AsNoTracking().ToListAsync(cancellationToken);
        return AppPolicy.Of(rows);
    }

    private static void Fill(ClientPolicyEntry row, PublishPolicyRequest request)
    {
        var bundle = request.Bundle;
        row.MinimumVersion = request.MinimumVersion;
        row.LatestVersion = request.LatestVersion;
        row.BundleId = bundle?.Id;
        row.BundleUrl = bundle?.Url;
        row.BundleChecksum = bundle?.Checksum;
        row.BundleSignature = bundle?.Signature;
        row.BundleMinimumNativeVersion = bundle?.MinimumNativeVersion;
    }

    [LoggerMessage(
        EventName = "client_policy.publish.succeeded",
        Level = LogLevel.Information,
        Message = "Client policy of {Platform} published: minimum {MinimumVersion}, "
            + "latest {LatestVersion}, bundle {BundleId}.")]
    private static partial void LogPublished(
        ILogger logger,
        string platform,
        string? minimumVersion,
        string? latestVersion,
        string? bundleId);
}
