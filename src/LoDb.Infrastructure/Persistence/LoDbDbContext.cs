using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Infrastructure.Persistence.Billing;
using LoDb.Infrastructure.Persistence.Builds;
using LoDb.Infrastructure.Persistence.Contact;
using LoDb.Infrastructure.Persistence.Ddragon;
using LoDb.Infrastructure.Persistence.Outbox;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Infrastructure.Persistence.Scheduling;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// The database of the whole rewrite: the tables shared with the legacy stack, then the
/// tables each lot adds.
/// </summary>
/// <remarks>
/// Only the schema chantiers (L1.4, L4.1, L6.2) change entities, configurations and
/// migrations, one migration per lot; migrations stay additive until the contract phase.
/// </remarks>
public sealed class LoDbDbContext(DbContextOptions<LoDbDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Build> Builds => Set<Build>();

    public DbSet<BuildVote> BuildVotes => Set<BuildVote>();

    public DbSet<Donation> Donations => Set<Donation>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<ApiUsage> ApiUsage => Set<ApiUsage>();

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    public DbSet<DdragonAsset> DdragonAssets => Set<DdragonAsset>();

    public DbSet<DdragonVersion> DdragonVersions => Set<DdragonVersion>();

    public DbSet<PeriodicJobState> PeriodicJobs => Set<PeriodicJobState>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<EmailOutboxMessage> EmailOutbox => Set<EmailOutboxMessage>();

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoDbDbContext).Assembly);
        LegacyUtcTimestamps.Apply(modelBuilder.Model);
    }
}
