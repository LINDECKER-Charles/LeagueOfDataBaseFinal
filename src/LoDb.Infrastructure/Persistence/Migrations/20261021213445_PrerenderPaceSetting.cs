using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The operator's choice of pace for the pre-rendering pass, set live from the admin: a table
/// of one row, adaptive or manual with a target in renders a minute.
/// </summary>
/// <remarks>
/// Additive: a new table the legacy stack ignores, and the image before it never reads. Empty
/// until an operator first sets the pace: the pass then stays adaptive, as before.
/// </remarks>
public partial class PrerenderPaceSetting : Migration
{
    private const string Table = "prerender_pace_setting";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: Table,
            columns: table => new
            {
                id = table.Column<short>(type: "smallint", nullable: false),
                mode = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false),
                renders_per_minute = table.Column<int>(type: "integer", nullable: false),
                updated_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_prerender_pace_setting", x => x.id);
                table.CheckConstraint(
                    "ck_prerender_pace_setting_mode",
                    "mode IN ('adaptive', 'manual')");
                table.CheckConstraint(
                    "ck_prerender_pace_setting_rate",
                    "renders_per_minute > 0");
                table.CheckConstraint("ck_prerender_pace_setting_singleton", "id = 1");
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: Table);
    }
}
