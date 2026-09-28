using System.Globalization;
using LoDb.Domain.Builds.Votes;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Http;

/// <summary>
/// The audit events of the builds, shaped as the legacy stack shapes them: the build as the
/// target, named, and the direction of a vote in its meta.
/// </summary>
/// <remarks>
/// The journal reads the actor from the request, still signed in when it records.
/// </remarks>
internal sealed class BuildAudit(IAuditLog log)
{
    private const string ValueKey = "value";

    public Task CreatedAsync(Build build, CancellationToken cancellationToken) =>
        log.RecordAsync(Line(AuditAction.BuildCreate, build), cancellationToken);

    public Task UpdatedAsync(Build build, CancellationToken cancellationToken) =>
        log.RecordAsync(Line(AuditAction.BuildUpdate, build), cancellationToken);

    /// <param name="build">The removed build, whose id and name the line keeps.</param>
    /// <param name="cancellationToken">Not observed by the journal.</param>
    public Task DeletedAsync(Build build, CancellationToken cancellationToken) =>
        log.RecordAsync(Line(AuditAction.BuildDelete, build), cancellationToken);

    /// <param name="build">The build voted on.</param>
    /// <param name="cast">The direction sent, even when it withdrew the vote.</param>
    /// <param name="cancellationToken">Not observed by the journal.</param>
    public Task VotedAsync(Build build, VoteDirection cast, CancellationToken cancellationToken) =>
        log.RecordAsync(
            Line(AuditAction.BuildVote, build) with
            {
                Meta = new Dictionary<string, object?> { [ValueKey] = (int)cast },
            },
            cancellationToken);

    private static AuditEvent Line(AuditAction action, Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new AuditEvent
        {
            Action = action,
            Target = new AuditTarget(
                AuditTargetType.Build,
                build.Id.ToString(CultureInfo.InvariantCulture),
                build.Name),
        };
    }
}
