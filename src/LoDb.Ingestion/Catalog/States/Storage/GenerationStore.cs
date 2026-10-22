using System.Linq.Expressions;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.States;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Ingestion.Catalog.States.Storage;

/// <summary>
/// The generations of the index of the compiled fingerprint scheme: the current one, those a
/// rebuild left before its flip, and what a walk resumes from.
/// </summary>
/// <remarks>
/// The generations of other schemes are never touched: the image that compiles one keeps
/// reading its own, so that a rollback finds it in place.
/// </remarks>
internal sealed class GenerationStore(
    IDbContextFactory<LoDbDbContext> contexts,
    TimeProvider timeProvider)
{
    private static readonly Expression<Func<CatalogStateGeneration, bool>> Current =
        static generation => generation.Scheme == FingerprintScheme.Current && generation.IsCurrent;

    /// <summary>The current generation; null while none is.</summary>
    public Task<CatalogStateGeneration?> CurrentAsync(CancellationToken cancellationToken) =>
        ReadAsync(
            db => db.CatalogStateGenerations.AsNoTracking()
                .SingleOrDefaultAsync(Current, cancellationToken),
            cancellationToken);

    /// <summary>The generations a rebuild started, not made current yet, newest first.</summary>
    public Task<List<CatalogStateGeneration>> PendingAsync(CancellationToken cancellationToken) =>
        ReadAsync(
            db => db.CatalogStateGenerations.AsNoTracking()
                .Where(static generation =>
                    generation.Scheme == FingerprintScheme.Current && !generation.IsCurrent)
                .OrderByDescending(static generation => generation.Id)
                .ToListAsync(cancellationToken),
            cancellationToken);

    /// <summary>
    /// A new generation, not current yet, whose coverage starts at <paramref name="start"/>.
    /// </summary>
    public async Task<CatalogStateGeneration> CreateAsync(
        PatchVersion start,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var now = timeProvider.GetUtcNow();
        var generation = new CatalogStateGeneration
        {
            Scheme = FingerprintScheme.Current,
            FirstVersion = start.Value,
            BuiltAt = now,
            UpdatedAt = now,
        };
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            db.CatalogStateGenerations.Add(generation);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return generation;
    }

    /// <summary>Deletes generations, their versions and states with them.</summary>
    public async Task DeleteAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return;
        }

        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            await db.CatalogStateGenerations
                .Where(generation => ids.Contains(generation.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Makes <paramref name="generationId"/> current in place of <paramref name="previousId"/>,
    /// in one transaction, then deletes the latter: a reader sees one current generation,
    /// never two nor none.
    /// </summary>
    public async Task MakeCurrentAsync(
        long generationId,
        long? previousId,
        CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var transaction = await db.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await Row(db, previousId)
                    .ExecuteUpdateAsync(
                        static setters => setters.SetProperty(row => row.IsCurrent, false),
                        cancellationToken)
                    .ConfigureAwait(false);
                await Row(db, generationId)
                    .ExecuteUpdateAsync(
                        static setters => setters.SetProperty(row => row.IsCurrent, true),
                        cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await DeleteAsync(previousId is { } previous ? [previous] : [], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The versions the generation covers: the ordinal of the next one.</summary>
    public Task<int> VersionCountAsync(long generationId, CancellationToken cancellationToken) =>
        ReadAsync(
            db => db.CatalogStateVersions
                .CountAsync(version => version.GenerationId == generationId, cancellationToken),
            cancellationToken);

    /// <summary>Every state of the generation, as much of it as a walk resumes from.</summary>
    public Task<List<StoredState>> StatesAsync(
        long generationId,
        CancellationToken cancellationToken) =>
        ReadAsync(
            db => db.CatalogEntityStates.AsNoTracking()
                .Where(state => state.GenerationId == generationId)
                .Select(static state => new StoredState
                {
                    Resource = state.Resource,
                    Key = state.EntityKey,
                    Opener = state.OpenerVersion,
                    Last = state.LastVersion,
                    ClosedAt = state.ClosedAt,
                    Fingerprint = state.Fingerprint,
                })
                .ToListAsync(cancellationToken),
            cancellationToken);

    /// <summary>The coverage and the counts of a generation.</summary>
    public Task<GenerationSummary> SummaryAsync(
        long generationId,
        CancellationToken cancellationToken) =>
        ReadAsync(db => SummaryOfAsync(db, generationId, cancellationToken), cancellationToken);

    private static async Task<GenerationSummary> SummaryOfAsync(
        LoDbDbContext db,
        long generationId,
        CancellationToken cancellationToken)
    {
        var generation = await db.CatalogStateGenerations.AsNoTracking()
            .SingleAsync(row => row.Id == generationId, cancellationToken)
            .ConfigureAwait(false);
        var start = await db.CatalogStateVersions.AsNoTracking()
            .Where(version => version.GenerationId == generationId)
            .OrderBy(static version => version.Ordinal)
            .Select(static version => version.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var counts = await db.CatalogEntityStates.AsNoTracking()
            .Where(state => state.GenerationId == generationId)
            .GroupBy(static state => state.Resource)
            .Select(static group => new { Resource = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new GenerationSummary
        {
            GenerationId = generationId,
            CoverageStart = start is null ? null : PatchVersion.Parse(start),
            CoverageEnd = generation.LastVersion is { } end ? PatchVersion.Parse(end) : null,
            States = generation.StateCount,
            OpenStates = generation.OpenStateCount,
            ByResource = counts.ToDictionary(
                static count => count.Resource,
                static count => count.Count),
        };
    }

    // The generation's row; none for a null id.
    private static IQueryable<CatalogStateGeneration> Row(LoDbDbContext db, long? generationId) =>
        db.CatalogStateGenerations.Where(generation => generation.Id == generationId);

    private async Task<T> ReadAsync<T>(
        Func<LoDbDbContext, Task<T>> read,
        CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await read(db).ConfigureAwait(false);
        }
    }
}
