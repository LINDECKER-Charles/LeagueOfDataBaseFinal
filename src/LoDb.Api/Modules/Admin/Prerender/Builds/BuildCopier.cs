using System.Collections.Concurrent;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Infrastructure.PageStore;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Modules.Admin.Prerender.Builds;

/// <summary>
/// Copies a front build into the store, its whole hashed set, before the first page it
/// rendered: a page names its entry bundles only, the lazy chunks are named in the scripts,
/// and <c>front-builds</c> prunes a build's files some months after it.
/// </summary>
/// <remarks>
/// <para>
/// A build is copied once: <c>prerender_build</c> holds those already in the store, with what
/// each added (files and bytes). The pass trusts the build each page names, never a list
/// fetched earlier: the SSR may be redeployed in the middle of a pass of two weeks.
/// </para>
/// <para>
/// A failure of a file stops the copy, unrecorded: the next page copies the build again, and
/// the files already there answer 412, left as they are, still counted with the build from
/// what the failed copy wrote (in memory: after a restart, they are not). The store's errors
/// go up as they come (<see cref="IPageStore"/>); the SSR's, as a
/// <see cref="SsrUnavailableException"/>.
/// </para>
/// <para>
/// The pages in flight together share one copy of a build: the first to need it starts it,
/// the others wait for it.
/// </para>
/// </remarks>
internal sealed class BuildCopier(
    SsrClient ssr,
    IPageStore store,
    IDbContextFactory<LoDbDbContext> contexts,
    TimeProvider clock)
{
    private const string BuildKey = "pk_prerender_build";

    private readonly ConcurrentDictionary<string, bool> _stored = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Lazy<Task>> _copies =
        new(StringComparer.Ordinal);

    // The files each build's copies wrote, by name, until the build is recorded.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, long>> _written =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Makes sure <paramref name="build"/>, which rendered a page, is in the store; copies the
    /// build the SSR runs now when it is not.
    /// </summary>
    public async Task<BuildCopy> EnsureAsync(string build, CancellationToken cancellationToken)
    {
        if (await IsStoredAsync(build, cancellationToken))
        {
            return BuildCopy.Stored;
        }

        var listing = await ssr.ListBuildAsync(cancellationToken);
        await CopyOnceAsync(listing, cancellationToken);
        return listing.Build == build ? BuildCopy.Stored : BuildCopy.Superseded;
    }

    // One copy at a time per build; a failed one is let go, for the next page to try again.
    private async Task CopyOnceAsync(BuildListing listing, CancellationToken cancellationToken)
    {
        var copy = _copies.GetOrAdd(
            listing.Build,
            _ => new Lazy<Task>(() => CopyUnlessStoredAsync(listing, cancellationToken)));
        try
        {
            await copy.Value;
        }
        finally
        {
            _copies.TryRemove(KeyValuePair.Create(listing.Build, copy));
        }
    }

    private async Task CopyUnlessStoredAsync(
        BuildListing listing,
        CancellationToken cancellationToken)
    {
        if (!await IsStoredAsync(listing.Build, cancellationToken))
        {
            await CopyAsync(listing, cancellationToken);
        }
    }

    private async Task<bool> IsStoredAsync(string build, CancellationToken cancellationToken)
    {
        if (_stored.ContainsKey(build))
        {
            return true;
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var isStored = await db.PrerenderBuilds
            .AnyAsync(row => row.Hash == build, cancellationToken);
        if (isStored)
        {
            _stored[build] = true;
        }

        return isStored;
    }

    private async Task CopyAsync(BuildListing listing, CancellationToken cancellationToken)
    {
        var written = _written.GetOrAdd(listing.Build, static _ => new(StringComparer.Ordinal));
        foreach (var name in listing.Files)
        {
            var file = await ssr.GetBuildFileAsync(name, cancellationToken);
            var outcome = await store.PutAsync(BuildObjects.Of(file), cancellationToken);
            if (outcome == PageWriteOutcome.Written)
            {
                written.TryAdd(name, file.Body.Length);
            }
        }

        await RecordAsync(
            new PrerenderBuild
            {
                Hash = listing.Build,
                StoredAt = clock.GetUtcNow(),
                Files = written.Count,
                Bytes = written.Values.Sum(),
            },
            cancellationToken);
        _stored[listing.Build] = true;
        _written.TryRemove(listing.Build, out _);
    }

    // Another instance may have copied the same build meanwhile: its row stands.
    private async Task RecordAsync(PrerenderBuild row, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.PrerenderBuilds.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: BuildKey,
        })
        {
            // Already recorded: nothing to add.
        }
    }
}
