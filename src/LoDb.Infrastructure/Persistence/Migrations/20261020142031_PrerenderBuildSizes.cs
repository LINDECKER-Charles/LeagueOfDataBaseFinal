using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// What each copied front build added to the page store: its files and their bytes
/// (<c>prerender_build</c>), counted as the pass writes them, for the admin's estimate of what
/// the store holds and costs.
/// </summary>
/// <remarks>
/// Additive: two columns with a default, on a table the legacy stack ignores. A build copied
/// before them counts zero.
/// </remarks>
public partial class PrerenderBuildSizes : Migration
{
    private const string BuildTable = "prerender_build";
    private const string SizeCheck = "ck_prerender_build_size";
    private const string Files = "files";
    private const string Bytes = "bytes";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: Bytes,
            table: BuildTable,
            type: "bigint",
            nullable: false,
            defaultValue: 0L);
        migrationBuilder.AddColumn<int>(
            name: Files,
            table: BuildTable,
            type: "integer",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddCheckConstraint(
            name: SizeCheck,
            table: BuildTable,
            sql: "files >= 0 AND bytes >= 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: SizeCheck, table: BuildTable);
        migrationBuilder.DropColumn(name: Bytes, table: BuildTable);
        migrationBuilder.DropColumn(name: Files, table: BuildTable);
    }
}
