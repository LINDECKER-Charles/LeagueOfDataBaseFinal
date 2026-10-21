using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The automatic queue of the pre-rendering (<c>prerender_demand</c>): the pages of older
/// patches the visitors' SSR refused, how often, and where the queue stands with each; with
/// the index its worker takes the most requested pending pages by, and the one the admin counts
/// the pages written lately by.
/// </summary>
/// <remarks>
/// Additive: a new table the legacy stack ignores and the image before it never reads, empty
/// until the SSR refuses a page. A return to that image leaves it as it is.
/// </remarks>
public partial class PrerenderDemand : Migration
{
    private const string Table = "prerender_demand";
    private const string Timestamp = "timestamp with time zone";
    private const string Short = "character varying(16)";
    private const int ShortLength = 16;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateTable(migrationBuilder);
        migrationBuilder.CreateIndex(
            name: "ix_prerender_demand_next",
            table: Table,
            column: "demand",
            descending: [true],
            filter: "state = 'pending'");
        migrationBuilder.CreateIndex(
            name: "ix_prerender_demand_rendered",
            table: Table,
            column: "rendered_at",
            filter: "rendered_at IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: Table);

    private static void CreateTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: Table,
            columns: table => new
            {
                path = table.Column<string>(
                    type: "character varying(160)",
                    maxLength: 160,
                    nullable: false),
                version = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                locale = table.Column<string>(type: Short, maxLength: ShortLength, nullable: false),
                page_type = table.Column<string>(
                    type: Short,
                    maxLength: ShortLength,
                    nullable: false),
                demand = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                ssr_instance = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true),
                ssr_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                first_seen_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                state = table.Column<string>(type: Short, maxLength: ShortLength, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                epoch = table.Column<int>(type: "integer", nullable: true),
                rendered_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: true),
                updated_at = table.Column<DateTimeOffset>(
                    type: Timestamp,
                    nullable: false,
                    defaultValueSql: "now()"),
                reason = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_demand", x => x.path);
                table.CheckConstraint(
                    "ck_prerender_demand_counters",
                    "demand >= 0 AND ssr_count >= 0 AND attempts >= 0");
                table.CheckConstraint(
                    "ck_prerender_demand_state",
                    "state IN ('pending', 'rendering', 'done', 'failed', 'skipped')");
            });
}
