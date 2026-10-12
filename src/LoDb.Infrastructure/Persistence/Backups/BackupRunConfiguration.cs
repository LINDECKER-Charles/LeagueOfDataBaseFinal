using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoDb.Infrastructure.Persistence.Backups;

/// <remarks>
/// A writer outside the API fills this table: the checks hold its rows to what the admin
/// reads, a step of one database naming it and a step of the repository naming none.
/// </remarks>
internal sealed class BackupRunConfiguration : IEntityTypeConfiguration<BackupRun>
{
    private const int StepMaxLength = 16;

    public void Configure(EntityTypeBuilder<BackupRun> builder)
    {
        builder.ToTable("backup_run", static table =>
        {
            table.HasCheckConstraint(
                "ck_backup_run_step",
                "step IN ('backup', 'retention', 'restore', 'check')");
            table.HasCheckConstraint(
                "ck_backup_run_base",
                "(base IS NOT NULL) = (step IN ('backup', 'restore'))"
                    + " AND (base IS NULL OR base ~ '^[a-z0-9]+$')");
            table.HasCheckConstraint("ck_backup_run_period", "finished_at >= started_at");
            table.HasCheckConstraint(
                "ck_backup_run_bytes",
                "bytes_read >= 0 AND bytes_added >= 0 AND repository_bytes >= 0");
        });
        builder.HasKey(run => run.Id);

        builder.Property(run => run.Database)
            .HasColumnName("base")
            .HasMaxLength(BackupRun.DatabaseMaxLength);
        builder.Property(run => run.Step)
            .HasMaxLength(StepMaxLength)
            .HasConversion(BackupColumns.StepConverter);
        builder.Property(run => run.Detail).HasMaxLength(BackupRun.DetailMaxLength);
        builder.Property(run => run.RecordedAt).HasDefaultValueSql("now()");

        // The admin reads the history newest first, over a window of days.
        builder.HasIndex(run => run.StartedAt).HasDatabaseName("ix_backup_run_started_at");
    }
}
