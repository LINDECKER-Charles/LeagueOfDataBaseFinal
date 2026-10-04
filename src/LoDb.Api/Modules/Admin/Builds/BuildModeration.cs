using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>
/// Unpublishes and deletes builds of any account, each action recorded in the audit
/// journal.
/// </summary>
/// <remarks>
/// An unpublished build leaves the public pages and the trends, and stays visible to its
/// author and through its share link, as in the legacy admin.
/// </remarks>
internal sealed class BuildModeration(LoDbDbContext db, IAuditLog audit)
{
    public async Task<AccountProblem?> UnpublishAsync(int id, CancellationToken cancellationToken)
    {
        if (await db.Builds.FindAsync([id], cancellationToken) is not { } build)
        {
            return AdminProblems.NotFound(AdminProblems.BuildNotFound);
        }

        build.IsPublic = false;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(Line(AuditAction.AdminBuildHide, build), cancellationToken);
        return null;
    }

    public async Task<AccountProblem?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (await db.Builds.FindAsync([id], cancellationToken) is not { } build)
        {
            return AdminProblems.NotFound(AdminProblems.BuildNotFound);
        }

        // The votes go with it: their foreign key cascades.
        db.Builds.Remove(build);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(Line(AuditAction.AdminBuildDelete, build), cancellationToken);
        return null;
    }

    private static AuditEvent Line(AuditAction action, Build build) =>
        new() { Action = action, Target = AdminTargets.Of(build) };
}
