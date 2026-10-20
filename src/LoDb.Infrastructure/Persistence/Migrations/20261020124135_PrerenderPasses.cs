using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The tables of the pre-rendering of the catalogue: the passes (<c>prerender_pass</c>), their
/// units (<c>prerender_unit</c>) and the front builds already copied (<c>prerender_build</c>).
/// </summary>
/// <remarks>
/// Additive: new tables the legacy stack ignores, empty until a pass is launched.
/// </remarks>
public partial class PrerenderPasses : Migration
{
    private const string BuildTable = "prerender_build";
    private const string PassTable = "prerender_pass";
    private const string UnitTable = "prerender_unit";
    private const string Identity = "Npgsql:ValueGenerationStrategy";
    private const string Timestamp = "timestamp with time zone";
    private const string State = "character varying(16)";
    private const int StateLength = 16;
    private const string Now = "now()";

    private static readonly string[] UnitNextColumns = ["pass_id", "state", "priority"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateBuildTable(migrationBuilder);
        CreatePassTable(migrationBuilder);
        CreateUnitTable(migrationBuilder);

        migrationBuilder.CreateIndex(
            name: "ix_prerender_pass_created_at",
            table: PassTable,
            column: "created_at");
        migrationBuilder.CreateIndex(
            name: "ux_prerender_pass_active",
            table: PassTable,
            column: "is_active",
            unique: true,
            filter: "is_active");
        migrationBuilder.CreateIndex(
            name: "ix_prerender_unit_next",
            table: UnitTable,
            columns: UnitNextColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: BuildTable);
        migrationBuilder.DropTable(name: UnitTable);
        migrationBuilder.DropTable(name: PassTable);
    }

    private static void CreateBuildTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: BuildTable,
            columns: table => new
            {
                hash = table.Column<string>(
                    type: "character(64)",
                    fixedLength: true,
                    maxLength: 64,
                    nullable: false),
                stored_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_build", x => x.hash);
                table.CheckConstraint("ck_prerender_build_hash", "hash ~ '^[0-9a-f]{64}$'");
            });

    private static void CreatePassTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: PassTable,
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(Identity, NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                epoch = table.Column<int>(type: "integer", nullable: false),
                state = table.Column<string>(type: State, maxLength: StateLength, nullable: false),
                scope = table.Column<string>(
                    type: "jsonb",
                    nullable: false,
                    defaultValueSql: "'{}'::jsonb"),
                author_id = table.Column<int>(type: "integer", nullable: true),
                is_active = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    computedColumnSql: "state IN ('running', 'paused')",
                    stored: true),
                created_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
                finished_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: true),
                running_since = table.Column<DateTimeOffset>(type: Timestamp, nullable: true),
                active_seconds = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                pages_total = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                pages_written = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                pages_skipped = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                pages_failed = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                bytes_written = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_pass", x => x.id);
                table.CheckConstraint(
                    "ck_prerender_pass_counters",
                    "pages_total >= 0 AND pages_written >= 0 AND pages_skipped >= 0"
                        + " AND pages_failed >= 0 AND bytes_written >= 0"
                        + " AND active_seconds >= 0");
                table.CheckConstraint("ck_prerender_pass_epoch", "epoch > 0");
                table.CheckConstraint(
                    "ck_prerender_pass_finished",
                    "(finished_at IS NOT NULL) = (state IN ('completed', 'cancelled'))");
                table.CheckConstraint(
                    "ck_prerender_pass_running",
                    "(running_since IS NOT NULL) = (state = 'running')");
                table.CheckConstraint("ck_prerender_pass_scope", "jsonb_typeof(scope) = 'object'");
                table.CheckConstraint(
                    "ck_prerender_pass_state",
                    "state IN ('running', 'paused', 'completed', 'cancelled')");
            });

    private static void CreateUnitTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: UnitTable,
            columns: table => new
            {
                pass_id = table.Column<long>(type: "bigint", nullable: false),
                version = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                locale = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                state = table.Column<string>(type: State, maxLength: StateLength, nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                pages_total = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                pages_written = table.Column<int>(
                    type: "integer",
                    nullable: false,
                    defaultValue: 0),
                pages_skipped = table.Column<int>(
                    type: "integer",
                    nullable: false,
                    defaultValue: 0),
                pages_failed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                bytes_written = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                cursor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                updated_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_unit", x => new { x.pass_id, x.version, x.locale });
                table.CheckConstraint(
                    "ck_prerender_unit_counters",
                    "pages_total >= 0 AND pages_written >= 0 AND pages_skipped >= 0"
                        + " AND pages_failed >= 0 AND bytes_written >= 0 AND cursor >= 0");
                table.CheckConstraint(
                    "ck_prerender_unit_state",
                    "state IN ('pending', 'running', 'completed', 'failed')");
                table.ForeignKey(
                    name: "fk_prerender_unit_prerender_pass_pass_id",
                    column: x => x.pass_id,
                    principalTable: PassTable,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });
}
