using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// Lot 1: the image manifest (<c>ddragon_asset</c>), the ingestion state of the versions
/// (<c>ddragon_version</c>) and the shared schedule of the periodic jobs
/// (<c>periodic_job</c>). New tables only, which the legacy stack ignores.
/// </summary>
public partial class Lot1DataDragon : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ddragon_asset",
            columns: table => new
            {
                version = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                type = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                key = table.Column<string>(
                    type: "character varying(255)",
                    maxLength: 255,
                    nullable: false),
                status = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                sha256 = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true),
                extension = table.Column<string>(
                    type: "character varying(8)",
                    maxLength: 8,
                    nullable: true),
                recorded_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ddragon_asset", x => new { x.version, x.type, x.key });
                table.CheckConstraint(
                    "ck_ddragon_asset_blob",
                    "(status = 'present' AND sha256 IS NOT NULL AND extension IS NOT NULL)"
                    + " OR (status = 'absent' AND sha256 IS NULL AND extension IS NULL)");
                table.CheckConstraint(
                    "ck_ddragon_asset_extension",
                    "extension ~ '^[a-z0-9]{1,8}$'");
                table.CheckConstraint("ck_ddragon_asset_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint(
                    "ck_ddragon_asset_status",
                    "status IN ('present', 'absent')");
            });

        migrationBuilder.CreateTable(
            name: "ddragon_version",
            columns: table => new
            {
                version = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                status = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                next_attempt_at = Timestamp(table, nullable: true),
                discovered_at = Timestamp(table, nullable: false),
                updated_at = Timestamp(table, nullable: false),
                ready_at = Timestamp(table, nullable: true),
                promoted_at = Timestamp(table, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ddragon_version", x => x.version);
                table.CheckConstraint("ck_ddragon_version_attempts", "attempts >= 0");
                table.CheckConstraint(
                    "ck_ddragon_version_status",
                    "status IN ('discovered', 'ingesting', 'ready', 'failed')");
            });

        migrationBuilder.CreateTable(
            name: "periodic_job",
            columns: table => new
            {
                name = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                last_started_at = Timestamp(table, nullable: false),
                last_succeeded_at = Timestamp(table, nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_periodic_job", x => x.name));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ddragon_asset");
        migrationBuilder.DropTable(name: "ddragon_version");
        migrationBuilder.DropTable(name: "periodic_job");
    }

    private static OperationBuilder<AddColumnOperation> Timestamp(
        ColumnsBuilder table,
        bool nullable) =>
        table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: nullable);
}
