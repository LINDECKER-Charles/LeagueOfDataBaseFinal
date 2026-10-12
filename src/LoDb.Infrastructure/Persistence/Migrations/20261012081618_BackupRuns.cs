using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The journal of the host's off-host backups (<c>backup_run</c>): one row per step of a
/// pass, written by the backup container of infra-vps, read by the admin.
/// </summary>
/// <remarks>
/// Additive: a new table, which the legacy stack ignores. It starts empty and stays so until
/// the backups are active and their container reports to it; the role that writes it gets
/// its single grant from <c>schema-privileges.sql</c>, at the end of the same run.
/// </remarks>
public partial class BackupRuns : Migration
{
    private const string Table = "backup_run";
    private const string Identity = "Npgsql:ValueGenerationStrategy";
    private const string Timestamp = "timestamp with time zone";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: Table,
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(Identity, NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                @base = table.Column<string>(
                    name: "base",
                    type: "character varying(63)",
                    maxLength: 63,
                    nullable: true),
                step = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                started_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                succeeded = table.Column<bool>(type: "boolean", nullable: false),
                bytes_read = table.Column<long>(type: "bigint", nullable: true),
                bytes_added = table.Column<long>(type: "bigint", nullable: true),
                repository_bytes = table.Column<long>(type: "bigint", nullable: true),
                detail = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true),
                recorded_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backup_run", x => x.id);
                table.CheckConstraint(
                    "ck_backup_run_base",
                    "(base IS NOT NULL) = (step IN ('backup', 'restore'))"
                        + " AND (base IS NULL OR base ~ '^[a-z0-9]+$')");
                table.CheckConstraint(
                    "ck_backup_run_bytes",
                    "bytes_read >= 0 AND bytes_added >= 0 AND repository_bytes >= 0");
                table.CheckConstraint("ck_backup_run_period", "finished_at >= started_at");
                table.CheckConstraint(
                    "ck_backup_run_step",
                    "step IN ('backup', 'retention', 'restore', 'check')");
            });
        migrationBuilder.CreateIndex(
            name: "ix_backup_run_started_at",
            table: Table,
            column: "started_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: Table);
}
