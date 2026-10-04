using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Builds;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Builds.Storage;

/// <summary>
/// The builds an account owns. Another account's build reads as an unknown one: the owner
/// alone learns that an id exists.
/// </summary>
internal sealed class OwnedBuilds(LoDbDbContext db)
{
    /// <summary>The build, tracked for a change; null when unknown or another's.</summary>
    public Task<Build?> FindAsync(int id, int ownerId, CancellationToken cancellationToken) =>
        db.Builds.SingleOrDefaultAsync(
            build => build.Id == id && build.OwnerId == ownerId,
            cancellationToken);
}
