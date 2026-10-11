using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// Takes the identifier typed on a failed sign-in out of the journal's older entries: its
/// text in <c>meta.identifier</c>, and in the target the legacy journal also wrote it to.
/// </summary>
/// <remarks>
/// The journal now keeps a kind and a keyed digest instead (<c>identifier_kind</c>,
/// <c>identifier_digest</c>), which the older entries cannot be given without the key. Data
/// only, the schema stays as it is; running it again changes nothing, and nothing brings
/// the text back down.
/// </remarks>
public partial class AuditIdentifierScrub : Migration
{
    private const string Scrub = """
        UPDATE audit_log
        SET meta = meta - 'identifier', target = NULL
        WHERE action = 'user.login_failed'
          AND ((meta IS NOT NULL AND meta ? 'identifier') OR target IS NOT NULL);
        """;

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(Scrub);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Nothing to restore: the text is gone on purpose.
    }
}
