using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// Lot 4: Identity on <c>users</c> (stamps, lockout, e-mail and username normalized by
/// PostgreSQL), its roles and tokens, the Data Protection key ring (<c>data_protection_keys</c>),
/// the e-mail outbox (<c>email_outbox</c>) and the audit journal (<c>audit_log</c>).
/// </summary>
/// <remarks>
/// Additive: the legacy columns of <c>users</c> keep their type, size and default, and each
/// new column is nullable, has a default or is generated, so the legacy stack goes on
/// inserting accounts without knowing them. The two generated columns rewrite
/// <c>users</c> once, under its lock.
/// </remarks>
public partial class Lot4Accounts : Migration
{
    private const string Users = "users";

    private static readonly string[] UserColumns =
    [
        "access_failed_count", "concurrency_stamp", "lockout_enabled", "lockout_end",
        "security_stamp", "two_factor_enabled", "normalized_email", "normalized_username",
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddUserColumns(migrationBuilder);
        CreateIdentityTables(migrationBuilder);
        CreateDataProtectionKeys(migrationBuilder);
        CreateEmailOutbox(migrationBuilder);
        CreateAuditLog(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_log");
        migrationBuilder.DropTable(name: "data_protection_keys");
        migrationBuilder.DropTable(name: "email_outbox");
        migrationBuilder.DropTable(name: "identity_user_roles");
        migrationBuilder.DropTable(name: "identity_user_tokens");
        migrationBuilder.DropTable(name: "identity_roles");
        migrationBuilder.DropIndex(name: "ix_users_normalized_email", table: Users);
        migrationBuilder.DropIndex(name: "ix_users_normalized_username", table: Users);
        foreach (var column in UserColumns)
        {
            migrationBuilder.DropColumn(name: column, table: Users);
        }
    }

    private static void AddUserColumns(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "access_failed_count",
            table: Users,
            type: "integer",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<string>(
            name: "concurrency_stamp",
            table: Users,
            type: "text",
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "lockout_enabled",
            table: Users,
            type: "boolean",
            nullable: false,
            defaultValue: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "lockout_end",
            table: Users,
            type: "timestamp with time zone",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "security_stamp",
            table: Users,
            type: "text",
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "two_factor_enabled",
            table: Users,
            type: "boolean",
            nullable: false,
            defaultValue: false);
        AddNormalizedColumn(migrationBuilder, "normalized_email", "upper((email)::text)");
        AddNormalizedColumn(migrationBuilder, "normalized_username", "upper((username)::text)");
    }

    private static void AddNormalizedColumn(
        MigrationBuilder migrationBuilder,
        string name,
        string expression)
    {
        migrationBuilder.AddColumn<string>(
            name: name,
            table: Users,
            type: "text",
            nullable: true,
            computedColumnSql: expression,
            stored: true);
        migrationBuilder.CreateIndex(name: "ix_users_" + name, table: Users, column: name);
    }

    private static void CreateIdentityTables(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identity_roles",
            columns: table => new
            {
                id = Identity<int>(table, "integer"),
                name = Varchar(table, 256, nullable: false),
                normalized_name = Varchar(table, 256, nullable: false),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_identity_roles", x => x.id));
        migrationBuilder.CreateIndex(
            name: "ix_identity_roles_normalized_name",
            table: "identity_roles",
            column: "normalized_name",
            unique: true);
        CreateUserTokens(migrationBuilder);
        CreateUserRoles(migrationBuilder);
    }

    private static void CreateUserTokens(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "identity_user_tokens",
            columns: table => new
            {
                user_id = table.Column<int>(type: "integer", nullable: false),
                login_provider = Varchar(table, 128, nullable: false),
                name = Varchar(table, 128, nullable: false),
                value = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_identity_user_tokens",
                    x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_identity_user_tokens_users_user_id",
                    column: x => x.user_id,
                    principalTable: Users,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void CreateUserRoles(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "identity_user_roles",
            columns: table => new
            {
                user_id = table.Column<int>(type: "integer", nullable: false),
                role_id = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identity_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "fk_identity_user_roles_identity_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "identity_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_identity_user_roles_users_user_id",
                    column: x => x.user_id,
                    principalTable: Users,
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(
            name: "ix_identity_user_roles_role_id",
            table: "identity_user_roles",
            column: "role_id");
    }

    private static void CreateDataProtectionKeys(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "data_protection_keys",
            columns: table => new
            {
                id = Identity<int>(table, "integer"),
                friendly_name = table.Column<string>(type: "text", nullable: true),
                xml = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_data_protection_keys", x => x.id));

    private static void CreateEmailOutbox(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "email_outbox",
            columns: table => new
            {
                id = Identity<long>(table, "bigint"),
                recipient = Varchar(table, 180, nullable: false),
                template = Varchar(table, 32, nullable: false),
                locale = Varchar(table, 8, nullable: false),
                model = table.Column<string>(type: "jsonb", nullable: false),
                status = Varchar(table, 16, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                next_attempt_at = Timestamp(table, nullable: false),
                created_at = Timestamp(table, nullable: false),
                last_attempt_at = Timestamp(table, nullable: true),
                sent_at = Timestamp(table, nullable: true),
                last_error_code = Varchar(table, 64, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_email_outbox", x => x.id);
                table.CheckConstraint("ck_email_outbox_attempts", "attempts >= 0");
                table.CheckConstraint(
                    "ck_email_outbox_status",
                    "status IN ('pending', 'sent', 'dead')");
            });
        migrationBuilder.CreateIndex(
            name: "ix_email_outbox_pending",
            table: "email_outbox",
            column: "next_attempt_at",
            filter: "status = 'pending'");
    }

    private static void CreateAuditLog(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_log",
            columns: table => new
            {
                id = Identity<long>(table, "bigint"),
                occurred_at = Timestamp(table, nullable: false),
                actor_type = Varchar(table, 16, nullable: false),
                actor_id = table.Column<int>(type: "integer", nullable: true),
                actor = Varchar(table, 180, nullable: true),
                action = Varchar(table, 64, nullable: false),
                outcome = Varchar(table, 16, nullable: false),
                target_type = Varchar(table, 32, nullable: true),
                target_id = Varchar(table, 64, nullable: true),
                target = Varchar(table, 255, nullable: true),
                ip = Varchar(table, 45, nullable: true),
                route = Varchar(table, 255, nullable: true),
                meta = table.Column<string>(type: "jsonb", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_log", x => x.id);
                table.CheckConstraint(
                    "ck_audit_log_actor_type",
                    "actor_type IN ('user', 'admin', 'anonymous')");
                table.CheckConstraint(
                    "ck_audit_log_outcome",
                    "outcome IN ('success', 'failure', 'denied')");
            });
        IndexAuditLog(migrationBuilder);
    }

    private static void IndexAuditLog(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_audit_log_action",
            table: "audit_log",
            columns: ["action", "occurred_at"]);
        migrationBuilder.CreateIndex(
            name: "ix_audit_log_actor",
            table: "audit_log",
            columns: ["actor_id", "occurred_at"]);
        migrationBuilder.CreateIndex(
            name: "ix_audit_log_occurred_at",
            table: "audit_log",
            column: "occurred_at");
    }

    private static OperationBuilder<AddColumnOperation> Identity<T>(
        ColumnsBuilder table,
        string type) =>
        table.Column<T>(type: type, nullable: false).Annotation(
            "Npgsql:ValueGenerationStrategy",
            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

    private static OperationBuilder<AddColumnOperation> Varchar(
        ColumnsBuilder table,
        int maxLength,
        bool nullable) =>
        table.Column<string>(
            type: $"character varying({maxLength})",
            maxLength: maxLength,
            nullable: nullable);

    private static OperationBuilder<AddColumnOperation> Timestamp(
        ColumnsBuilder table,
        bool nullable) =>
        table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: nullable);
}
