using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The families of refresh tokens of the apps (<c>refresh_token_families</c>): the generation
/// whose token refreshes now, so that a refresh token serves once and one played twice
/// revokes its family, and the revocations of a sign-out.
/// </summary>
/// <remarks>
/// Additive: a new table, which the legacy stack ignores, empty at first. A refresh token
/// handed out before it names no family and gets one at its first refresh.
/// </remarks>
public partial class RefreshTokenFamilies : Migration
{
    private const string Table = "refresh_token_families";
    private const string Instant = "timestamp with time zone";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: Table,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<int>(type: "integer", nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: Instant, nullable: false),
                rotated_at = table.Column<DateTimeOffset>(type: Instant, nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: Instant, nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: Instant, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_token_families", x => x.id);
                table.ForeignKey(
                    name: "fk_refresh_token_families_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_refresh_token_families_expires_at",
            table: Table,
            column: "expires_at");
        migrationBuilder.CreateIndex(
            name: "ix_refresh_token_families_user_id",
            table: Table,
            column: "user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: Table);
}
