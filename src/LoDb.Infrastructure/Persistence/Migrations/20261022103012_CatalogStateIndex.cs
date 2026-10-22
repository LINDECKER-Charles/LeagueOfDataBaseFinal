using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The index of entity states (docs/produit/seo-une-page-par-etat.md, § 3.2): its generations
/// (<c>catalog_state_generation</c>), the versions each covers (<c>catalog_state_version</c>)
/// and the states (<c>catalog_entity_state</c>); and the re-render of the pages whose state a
/// promotion closes (<c>prerender_revision</c>, <c>prerender_revision_watermark</c>).
/// </summary>
/// <remarks>
/// Additive: new tables the legacy stack ignores and the image before it never reads, empty
/// until the index is built. A return to that image leaves them as they are.
/// </remarks>
public partial class CatalogStateIndex : Migration
{
    private const string GenerationTable = "catalog_state_generation";
    private const string VersionTable = "catalog_state_version";
    private const string StateTable = "catalog_entity_state";
    private const string RevisionTable = "prerender_revision";
    private const string WatermarkTable = "prerender_revision_watermark";
    private const string Identity = "Npgsql:ValueGenerationStrategy";
    private const string Timestamp = "timestamp with time zone";
    private const string Version = "character varying(32)";
    private const int VersionLength = 32;
    private const string Short = "character varying(16)";
    private const int ShortLength = 16;
    private const string Now = "now()";

    private static readonly string[] OpenColumns = ["generation_id", "resource", "entity_key"];
    private static readonly string[] OpenerColumns =
        ["generation_id", "resource", "entity_key", "opener_version"];
    private static readonly string[] ClosedColumns = ["generation_id", "closed_at"];
    private static readonly string[] OrdinalColumns = ["generation_id", "ordinal"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateGenerationTable(migrationBuilder);
        CreateVersionTable(migrationBuilder);
        CreateStateTable(migrationBuilder);
        CreateRevisionTable(migrationBuilder);
        CreateWatermarkTable(migrationBuilder);
        CreateIndexIndexes(migrationBuilder);
        CreateRevisionIndexes(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: StateTable);
        migrationBuilder.DropTable(name: VersionTable);
        migrationBuilder.DropTable(name: RevisionTable);
        migrationBuilder.DropTable(name: WatermarkTable);
        migrationBuilder.DropTable(name: GenerationTable);
    }

    private static void CreateGenerationTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: GenerationTable,
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(Identity, NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                scheme = table.Column<int>(type: "integer", nullable: false),
                first_version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: false),
                last_version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: true),
                built_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
                updated_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
                is_current = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    defaultValue: false),
                state_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                open_state_count = table.Column<int>(
                    type: "integer",
                    nullable: false,
                    defaultValue: 0),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_catalog_state_generation", x => x.id);
                table.CheckConstraint(
                    "ck_catalog_state_generation_counters",
                    "state_count >= 0 AND open_state_count >= 0"
                        + " AND open_state_count <= state_count");
                table.CheckConstraint("ck_catalog_state_generation_scheme", "scheme > 0");
            });

    private static void CreateVersionTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: VersionTable,
            columns: table => new
            {
                generation_id = table.Column<long>(type: "bigint", nullable: false),
                version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: false),
                ordinal = table.Column<int>(type: "integer", nullable: false),
                indexed_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_catalog_state_version",
                    x => new { x.generation_id, x.version });
                table.CheckConstraint("ck_catalog_state_version_ordinal", "ordinal >= 0");
                table.ForeignKey(
                    name: "fk_catalog_state_version_catalog_state_generation_generation_id",
                    column: x => x.generation_id,
                    principalTable: GenerationTable,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void CreateStateTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: StateTable,
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(Identity, NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                generation_id = table.Column<long>(type: "bigint", nullable: false),
                resource = table.Column<string>(
                    type: Short,
                    maxLength: ShortLength,
                    nullable: false),
                entity_key = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                opener_version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: false),
                last_version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: true),
                closed_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: true),
                fingerprint = table.Column<string>(
                    type: "character(64)",
                    fixedLength: true,
                    maxLength: 64,
                    nullable: false),
                path = table.Column<string>(
                    type: "character varying(160)",
                    maxLength: 160,
                    nullable: false),
                previous_opener_version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: true),
                is_return = table.Column<bool>(
                    type: "boolean",
                    nullable: false,
                    defaultValue: false),
                changed_fields = table.Column<string[]>(
                    type: "text[]",
                    nullable: false,
                    defaultValueSql: "'{}'::text[]"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_catalog_entity_state", x => x.id);
                AddStateChecks(table);
                table.ForeignKey(
                    name: "fk_catalog_entity_state_catalog_state_generation_generation_id",
                    column: x => x.generation_id,
                    principalTable: GenerationTable,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void AddStateChecks<TColumns>(CreateTableBuilder<TColumns> table)
    {
        table.CheckConstraint(
            "ck_catalog_entity_state_closed",
            "(closed_at IS NULL) = (last_version IS NULL)");
        table.CheckConstraint(
            "ck_catalog_entity_state_fingerprint",
            "fingerprint ~ '^[0-9a-f]{64}$'");
        table.CheckConstraint(
            "ck_catalog_entity_state_new",
            "previous_opener_version IS NOT NULL OR cardinality(changed_fields) = 0");
        table.CheckConstraint(
            "ck_catalog_entity_state_resource",
            "resource IN ('champions', 'items', 'runes', 'summoners')");
        table.CheckConstraint(
            "ck_catalog_entity_state_return",
            "NOT is_return OR previous_opener_version IS NOT NULL");
    }

    private static void CreateRevisionTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: RevisionTable,
            columns: table => new
            {
                path = table.Column<string>(
                    type: "character varying(160)",
                    maxLength: 160,
                    nullable: false),
                epoch = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<string>(
                    type: Version,
                    maxLength: VersionLength,
                    nullable: false),
                locale = table.Column<string>(type: Short, maxLength: ShortLength, nullable: false),
                state = table.Column<string>(type: Short, maxLength: ShortLength, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                enqueued_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
                due_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
                written_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: true),
                updated_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
                reason = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_revision", x => new { x.path, x.epoch });
                table.CheckConstraint("ck_prerender_revision_attempts", "attempts >= 0");
                table.CheckConstraint("ck_prerender_revision_epoch", "epoch > 0");
                table.CheckConstraint(
                    "ck_prerender_revision_state",
                    "state IN ('pending', 'rendering', 'done', 'failed', 'skipped')");
            });

    private static void CreateWatermarkTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: WatermarkTable,
            columns: table => new
            {
                id = table.Column<short>(type: "smallint", nullable: false),
                generation_id = table.Column<long>(type: "bigint", nullable: false),
                closed_through = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                updated_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: Now),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_revision_watermark", x => x.id);
                table.CheckConstraint("ck_prerender_revision_watermark_singleton", "id = 1");
            });

    private static void CreateIndexIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_catalog_entity_state_closed",
            table: StateTable,
            columns: ClosedColumns,
            filter: "closed_at IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "ux_catalog_entity_state_open",
            table: StateTable,
            columns: OpenColumns,
            unique: true,
            filter: "closed_at IS NULL");
        migrationBuilder.CreateIndex(
            name: "ux_catalog_entity_state_opener",
            table: StateTable,
            columns: OpenerColumns,
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ux_catalog_state_generation_current",
            table: GenerationTable,
            column: "scheme",
            unique: true,
            filter: "is_current");
        migrationBuilder.CreateIndex(
            name: "ux_catalog_state_version_ordinal",
            table: VersionTable,
            columns: OrdinalColumns,
            unique: true);
    }

    private static void CreateRevisionIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_prerender_revision_next",
            table: RevisionTable,
            column: "due_at",
            filter: "state = 'pending'");
        migrationBuilder.CreateIndex(
            name: "ix_prerender_revision_written",
            table: RevisionTable,
            column: "written_at",
            filter: "written_at IS NOT NULL");
    }
}
