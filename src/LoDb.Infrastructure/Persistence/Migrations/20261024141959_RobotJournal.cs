using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The robots journal: the user agents met (<c>robot_agent</c>), the hits kept 30 days
/// (<c>robot_hit</c>) and the daily counts kept with no end (<c>robot_daily</c>).
/// </summary>
/// <remarks>
/// Additive: new tables the legacy stack and the image before this one ignore, filled once
/// nginx reports to the journal.
/// </remarks>
public partial class RobotJournal : Migration
{
    private const string AgentTable = "robot_agent";
    private const string HitTable = "robot_hit";
    private const string DayTable = "robot_daily";
    private const string Identity = "Npgsql:ValueGenerationStrategy";
    private const string Timestamp = "timestamp with time zone";
    private const string Label = "character varying(16)";
    private const int LabelLength = 16;
    private const string Network = "character varying(253)";
    private const int NetworkLength = 253;
    private const string Detections = "detection IN ('agent', 'rate')";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateAgentTable(migrationBuilder);
        CreateDayTable(migrationBuilder);
        CreateHitTable(migrationBuilder);
        migrationBuilder.CreateIndex(
            name: "ix_robot_daily_agent_day",
            table: DayTable,
            columns: ["agent_id", "day"]);
        migrationBuilder.CreateIndex(
            name: "ix_robot_hit_agent_occurred_at",
            table: HitTable,
            columns: ["agent_id", "occurred_at"]);
        migrationBuilder.CreateIndex(
            name: "ix_robot_hit_occurred_at",
            table: HitTable,
            column: "occurred_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: DayTable);
        migrationBuilder.DropTable(name: HitTable);
        migrationBuilder.DropTable(name: AgentTable);
    }

    private static void CreateAgentTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: AgentTable,
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(Identity, NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                agent = table.Column<string>(
                    type: "character varying(512)",
                    maxLength: 512,
                    nullable: false),
                first_seen = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                last_seen = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_robot_agent", agent => agent.id));
        migrationBuilder.CreateIndex(
            name: "ux_robot_agent_agent",
            table: AgentTable,
            column: "agent",
            unique: true);
    }

    private static void CreateDayTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: DayTable,
            columns: table => new
            {
                day = table.Column<DateOnly>(type: "date", nullable: false),
                agent_id = table.Column<int>(type: "integer", nullable: false),
                detection = LabelColumn(table),
                verdict = LabelColumn(table),
                network = table.Column<string>(
                    type: Network,
                    maxLength: NetworkLength,
                    nullable: false),
                section = LabelColumn(table),
                status = table.Column<short>(type: "smallint", nullable: false),
                hits = table.Column<long>(type: "bigint", nullable: false),
                bytes = table.Column<long>(type: "bigint", nullable: false),
                duration_ms = table.Column<long>(type: "bigint", nullable: false),
                slow_hits = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_robot_daily", day => new
                {
                    day.day,
                    day.agent_id,
                    day.detection,
                    day.verdict,
                    day.network,
                    day.section,
                    day.status,
                });
                table.CheckConstraint(
                    "ck_robot_daily_counts",
                    "hits >= 0 AND bytes >= 0 AND duration_ms >= 0 AND slow_hits >= 0");
                table.CheckConstraint("ck_robot_daily_detection", Detections);
                table.ForeignKey(
                    name: "fk_robot_daily_robot_agent_agent_id",
                    column: day => day.agent_id,
                    principalTable: AgentTable,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

    private static void CreateHitTable(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: HitTable,
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(Identity, NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                occurred_at = table.Column<DateTimeOffset>(type: Timestamp, nullable: false),
                agent_id = table.Column<int>(type: "integer", nullable: false),
                detection = LabelColumn(table),
                verdict = LabelColumn(table),
                network = table.Column<string>(
                    type: Network,
                    maxLength: NetworkLength,
                    nullable: true),
                method = LabelColumn(table),
                target = table.Column<string>(
                    type: "character varying(1024)",
                    maxLength: 1024,
                    nullable: false),
                section = LabelColumn(table),
                status = table.Column<short>(type: "smallint", nullable: false),
                duration_ms = table.Column<int>(type: "integer", nullable: false),
                bytes = table.Column<long>(type: "bigint", nullable: false),
                cache = table.Column<string>(type: Label, maxLength: LabelLength, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_robot_hit", hit => hit.id);
                table.CheckConstraint("ck_robot_hit_detection", Detections);
                table.CheckConstraint("ck_robot_hit_duration", "duration_ms >= 0");
                table.ForeignKey(
                    name: "fk_robot_hit_robot_agent_agent_id",
                    column: hit => hit.agent_id,
                    principalTable: AgentTable,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

    private static OperationBuilder<AddColumnOperation> LabelColumn(ColumnsBuilder table) =>
        table.Column<string>(type: Label, maxLength: LabelLength, nullable: false);
}
