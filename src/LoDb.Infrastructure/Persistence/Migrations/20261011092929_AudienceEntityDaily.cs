using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The page views and visitors that Umami counted per entity and per UTC day
/// (<c>audience_entity_daily</c>), pulled every hour for <c>/v1/trends</c>.
/// </summary>
/// <remarks>
/// Additive: a new table, which the legacy stack ignores. It starts empty: the trends read
/// <c>analytics_daily</c> until the switch date the configuration sets, and this table after it.
/// </remarks>
public partial class AudienceEntityDaily : Migration
{
    private const string Table = "audience_entity_daily";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: Table,
            columns: table => new
            {
                day = table.Column<DateOnly>(type: "date", nullable: false),
                entity = table.Column<string>(
                    type: "character varying(161)",
                    maxLength: 161,
                    nullable: false),
                views = table.Column<long>(type: "bigint", nullable: false),
                visitors = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audience_entity_daily", x => new { x.day, x.entity });
                table.CheckConstraint("ck_audience_entity_daily_views", "views >= 0");
                table.CheckConstraint("ck_audience_entity_daily_visitors", "visitors >= 0");
            });
        migrationBuilder.CreateIndex(
            name: "ix_audience_entity_daily_entity_day",
            table: Table,
            columns: ["entity", "day"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: Table);
}
