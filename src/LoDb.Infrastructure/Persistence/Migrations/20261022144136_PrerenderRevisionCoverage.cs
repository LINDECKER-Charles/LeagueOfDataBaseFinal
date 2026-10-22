using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The coverage of the index the producer of the queue of the closed states last read
/// (<c>prerender_revision_watermark.covered_through</c>): on a rebuilt generation, the closures
/// past it are those no run enqueued, whatever date the rebuild gave them.
/// </summary>
/// <remarks>
/// Additive: a nullable column on a table the legacy stack ignores and the image before it
/// never reads. A row written before it keeps null, and the producer then takes a rebuilt
/// generation as history but for its last promotion, as it did.
/// </remarks>
public partial class PrerenderRevisionCoverage : Migration
{
    private const string Table = "prerender_revision_watermark";
    private const string Column = "covered_through";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: Column,
            table: Table,
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: Column, table: Table);
    }
}
