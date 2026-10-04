using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Audit.Support;

/// <summary>Writes and reads <c>audit_log</c> straight through the host's context.</summary>
public static class JournalRows
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>An entry of a signed-in user at <paramref name="at"/>.</summary>
    public static AuditLogEntry Entry(DateTimeOffset at, AuditAction action) => new()
    {
        OccurredAt = at,
        ActorType = AuditActorType.User,
        ActorId = 7,
        Actor = "Joueur_7",
        Action = action,
        Outcome = AuditOutcome.Success,
        Ip = "203.0.113.9",
        Route = "/api/test",
    };

    /// <summary>Empties the journal: a sign-in already wrote to it.</summary>
    public static async Task ClearAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        await db.AuditLog.ExecuteDeleteAsync(Cancellation);
    }

    public static async Task AddAsync(IServiceProvider services, params AuditLogEntry[] entries)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        db.AuditLog.AddRange(entries);
        await db.SaveChangesAsync(Cancellation);
    }

    /// <summary>The journal, oldest first.</summary>
    public static async Task<IReadOnlyList<AuditLogEntry>> AllAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        return await db.AuditLog.AsNoTracking()
            .OrderBy(static entry => entry.OccurredAt)
            .ThenBy(static entry => entry.Id)
            .ToListAsync(Cancellation);
    }
}
