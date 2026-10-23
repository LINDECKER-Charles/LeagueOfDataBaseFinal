using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// What the automatic queue holds in the store (<c>prerender_demand</c>): the bytes and the
/// class of each page it wrote, the date a pass wrote that page again in the same epoch, and
/// the index the admin counts and weighs the queue's pages of each epoch by.
/// </summary>
/// <remarks>
/// Additive: three nullable columns and an index on a table the legacy stack ignores and the
/// image before it never reads. A page written before them keeps null bytes, counted as none.
/// </remarks>
public partial class PrerenderDemandLedger : Migration
{
    private const string Table = "prerender_demand";
    private const string Index = "ix_prerender_demand_stored";
    private const string Bytes = "bytes";
    private const string StoredClass = "stored_class";
    private const string SupersededAt = "superseded_at";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: Bytes, table: Table, type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: StoredClass,
            table: Table,
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: SupersededAt,
            table: Table,
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.CreateIndex(
                name: Index,
                table: Table,
                column: "epoch",
                filter: "state = 'done' AND superseded_at IS NULL")
            .Annotation("Npgsql:IndexInclude", new[] { Bytes });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: Index, table: Table);
        migrationBuilder.DropColumn(name: Bytes, table: Table);
        migrationBuilder.DropColumn(name: StoredClass, table: Table);
        migrationBuilder.DropColumn(name: SupersededAt, table: Table);
    }
}
