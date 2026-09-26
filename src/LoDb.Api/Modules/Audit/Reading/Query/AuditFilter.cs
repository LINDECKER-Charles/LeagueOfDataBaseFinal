using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Audit.Reading.Query;

/// <summary>The criteria of a journal query, every one optional, all of them applied.</summary>
internal sealed record AuditFilter
{
    public static AuditFilter None { get; } = new();

    /// <summary>The actions kept; null for every action, empty for none.</summary>
    public IReadOnlyList<AuditAction>? Actions { get; init; }

    public AuditOutcome? Outcome { get; init; }

    public AuditActorType? ActorType { get; init; }

    public int? ActorId { get; init; }

    /// <summary>The actor's username, whatever its case.</summary>
    public string? Actor { get; init; }

    /// <summary>First instant kept.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>First instant no longer kept.</summary>
    public DateTimeOffset? Until { get; init; }

    public IQueryable<AuditLogEntry> Apply(IQueryable<AuditLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (Actions is { } actions)
        {
            entries = entries.Where(entry => actions.Contains(entry.Action));
        }

        if (Outcome is { } outcome)
        {
            entries = entries.Where(entry => entry.Outcome == outcome);
        }

        if (ActorType is { } actorType)
        {
            entries = entries.Where(entry => entry.ActorType == actorType);
        }

        if (ActorId is { } actorId)
        {
            entries = entries.Where(entry => entry.ActorId == actorId);
        }

        if (Actor is { } actor)
        {
            // ILIKE with every wildcard escaped: a case-insensitive equality on the index-free
            // label, usernames holding "_" included.
            var pattern = LikeEscape.Escape(actor);
            entries = entries.Where(entry =>
                entry.Actor != null
                && EF.Functions.ILike(entry.Actor, pattern, LikeEscape.Character));
        }

        return ApplyPeriod(entries);
    }

    private IQueryable<AuditLogEntry> ApplyPeriod(IQueryable<AuditLogEntry> entries)
    {
        if (From is { } from)
        {
            entries = entries.Where(entry => entry.OccurredAt >= from);
        }

        if (Until is { } until)
        {
            entries = entries.Where(entry => entry.OccurredAt < until);
        }

        return entries;
    }
}
