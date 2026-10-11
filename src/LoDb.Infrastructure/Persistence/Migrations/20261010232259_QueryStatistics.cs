using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The statistics of the queries: the extension <c>pg_stat_statements</c>, whose view
/// (<c>SELECT … FROM pg_stat_statements</c>) gives the calls and the time of each statement.
/// </summary>
/// <remarks>
/// <para>
/// Additive: an extension and its objects, which neither stack reads and the schema catalog
/// leaves out (it skips what an extension owns). The server must load the library at start
/// (<c>shared_preload_libraries</c>, compose.yaml) for the view to answer; the extension
/// installs without it, so a server that does not load it still migrates.
/// </para>
/// <para>
/// Creating it takes a superuser, which the image's <c>POSTGRES_USER</c> is: a role without
/// privileges for the API can only come after it.
/// </para>
/// </remarks>
public partial class QueryStatistics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_stat_statements");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_stat_statements");
}
