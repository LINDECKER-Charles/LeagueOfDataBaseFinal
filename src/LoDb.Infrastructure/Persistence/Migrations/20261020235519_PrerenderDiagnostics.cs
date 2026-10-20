﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// What a pass now keeps for the admin, which only its log held: when each unit started and
/// ended, why it failed, the pages it should list, and since when and why its pass paused;
/// with the index the admin reads the units of a pass by, in the order they ended.
/// </summary>
/// <remarks>
/// Additive: nullable columns without a constraint tying them to the state, on tables the
/// legacy stack ignores. The image before them pauses and ends passes without filling them, and
/// a return to it must stay possible: the rows it writes keep them null. No index on
/// <c>updated_at</c>, rewritten at each page: <c>ended_at</c> is written once per unit.
/// </remarks>
public partial class PrerenderDiagnostics : Migration
{
    private const string UnitTable = "prerender_unit";
    private const string PassTable = "prerender_pass";
    private const string EndedIndex = "ix_prerender_unit_ended";
    private const string Timestamp = "timestamp with time zone";
    private const string Reason = "character varying(300)";
    private const int ReasonMaxLength = 300;
    private const string StartedAt = "started_at";
    private const string EndedAt = "ended_at";
    private const string Failure = "failure";
    private const string PagesExpected = "pages_expected";
    private const string PausedAt = "paused_at";
    private const string PauseReason = "pause_reason";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: StartedAt,
            table: UnitTable,
            type: Timestamp,
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: EndedAt,
            table: UnitTable,
            type: Timestamp,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: Failure,
            table: UnitTable,
            type: Reason,
            maxLength: ReasonMaxLength,
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: PagesExpected,
            table: UnitTable,
            type: "integer",
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: PausedAt,
            table: PassTable,
            type: Timestamp,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: PauseReason,
            table: PassTable,
            type: Reason,
            maxLength: ReasonMaxLength,
            nullable: true);
        migrationBuilder.CreateIndex(
            name: EndedIndex,
            table: UnitTable,
            columns: ["pass_id", EndedAt],
            filter: "ended_at IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: EndedIndex, table: UnitTable);
        migrationBuilder.DropColumn(name: StartedAt, table: UnitTable);
        migrationBuilder.DropColumn(name: EndedAt, table: UnitTable);
        migrationBuilder.DropColumn(name: Failure, table: UnitTable);
        migrationBuilder.DropColumn(name: PagesExpected, table: UnitTable);
        migrationBuilder.DropColumn(name: PausedAt, table: PassTable);
        migrationBuilder.DropColumn(name: PauseReason, table: PassTable);
    }
}
