using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// The schema the 11 Doctrine migrations create, exactly (reported in
/// <c>docs/reecriture/rapports/schema-baseline.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// On a database the legacy stack created, <c>migrate</c> records this migration without
/// running it (<c>baseline mark-applied</c>). On an empty database it creates the legacy
/// schema, Doctrine history included, so that the legacy stack still runs on it and replays
/// nothing. Its id comes one second after the last Doctrine migration.
/// </para>
/// <para>
/// The tables the new stack does not map and the <c>LOWER()</c> indexes are created by the
/// raw SQL of <c>20260719150001_Baseline.Legacy.cs</c>. Frozen: a change of schema is a new
/// migration.
/// </para>
/// </remarks>
public partial class Baseline : Migration
{
    private static readonly string[] UsageKeyDay = ["api_key_id", "day"];
    private static readonly string[] VoteBuildVoter = ["build_id", "voter_id"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateUsers(migrationBuilder);
        CreateApiKeys(migrationBuilder);
        CreateBuilds(migrationBuilder);
        CreateContactMessages(migrationBuilder);
        CreateDonations(migrationBuilder);
        CreateApiUsage(migrationBuilder);
        CreateBuildVotes(migrationBuilder);
        CreateIndexes(migrationBuilder);
        foreach (var statement in LegacyStatements)
        {
            migrationBuilder.Sql(statement);
        }
    }

    /// <summary>Never rolled back: the legacy tables hold the players' data.</summary>
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The baseline is never rolled back.");

    private static void CreateUsers(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                email = VarChar(table, 180, nullable: false),
                username = VarChar(table, 24, nullable: false),
                roles = table.Column<string>(type: "json", nullable: false),
                password = VarChar(table, 255, nullable: true),
                is_public_profile = Flag(table, defaultValue: false),
                favorite_champion_id = NullDefault(table, 64),
                favorite_item_id = NullDefault(table, 16),
                favorite_rune_id = NullDefault(table, 16),
                favorite_summoner_id = NullDefault(table, 64),
                created_at = LegacyTimestamp(table, nullable: false),
                google_id = NullDefault(table, 30),
                riot_tagline = NullDefault(table, 5),
                is_supporter = Flag(table, defaultValue: false),
                is_banned = Flag(table, defaultValue: false),
                banned_at = table.Column<DateTimeOffset>(
                    type: "timestamp(0) with time zone",
                    precision: 0,
                    nullable: true,
                    defaultValueSql: "NULL"),
                ban_reason = NullDefault(table, 255),
                is_verified = Flag(table, defaultValue: false),
                favorite_skin_id = NullDefault(table, 64),
                preferred_version = NullDefault(table, 24),
            },
            constraints: table => table.PrimaryKey("users_pkey", x => x.id));

    private static void CreateApiKeys(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "api_keys",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = VarChar(table, 64, nullable: false),
                key_hash = VarChar(table, 64, nullable: false),
                key_prefix = VarChar(table, 12, nullable: false),
                plan = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false,
                    defaultValue: "free"),
                monthly_quota = table.Column<int>(
                    type: "integer",
                    nullable: false,
                    defaultValue: 500),
                credits_balance = table.Column<long>(
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L),
                rate_limit_per_min = table.Column<int>(
                    type: "integer",
                    nullable: false,
                    defaultValue: 10),
                is_active = Flag(table, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp(0) with time zone",
                    precision: 0,
                    nullable: false),
                revoked_at = table.Column<DateTimeOffset>(
                    type: "timestamp(0) with time zone",
                    precision: 0,
                    nullable: true,
                    defaultValueSql: "NULL"),
                stripe_customer_id = NullDefault(table, 64),
                stripe_subscription_id = NullDefault(table, 64),
                user_id = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("api_keys_pkey", x => x.id);
                table.ForeignKey(
                    name: "fk_9579321fa76ed395",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void CreateBuilds(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "builds",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = VarChar(table, 80, nullable: false),
                champion_id = VarChar(table, 64, nullable: false),
                game_version = VarChar(table, 24, nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                runes = table.Column<string>(type: "jsonb", nullable: false),
                steps = table.Column<string>(type: "jsonb", nullable: false),
                is_public = Flag(table, defaultValue: false),
                share_token = VarChar(table, 24, nullable: false),
                created_at = LegacyTimestamp(table, nullable: false),
                updated_at = LegacyTimestamp(table, nullable: false),
                owner_id = table.Column<int>(type: "integer", nullable: false),
                game_mode = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false,
                    defaultValue: "sr"),
                language = table.Column<string>(
                    type: "character varying(8)",
                    maxLength: 8,
                    nullable: false,
                    defaultValue: "en_US"),
            },
            constraints: table =>
            {
                table.PrimaryKey("builds_pkey", x => x.id);
                table.ForeignKey(
                    name: "fk_ab264a57e3c61f9",
                    column: x => x.owner_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void CreateContactMessages(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "contact_messages",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                category = VarChar(table, 255, nullable: false),
                name = NullDefault(table, 120),
                email = VarChar(table, 255, nullable: false),
                subject = NullDefault(table, 160),
                message = table.Column<string>(type: "text", nullable: false),
                locale = NullDefault(table, 16),
                ip = NullDefault(table, 64),
                status = table.Column<string>(
                    type: "character varying(255)",
                    maxLength: 255,
                    nullable: false,
                    defaultValue: "new"),
                created_at = LegacyTimestamp(table, nullable: false),
                handled_at = table.Column<DateTime>(
                    type: "timestamp(0) without time zone",
                    nullable: true,
                    defaultValueSql: "NULL"),
                user_id = table.Column<int>(type: "integer", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("contact_messages_pkey", x => x.id);
                table.ForeignKey(
                    name: "fk_41278201a76ed395",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

    private static void CreateDonations(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "donations",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                stripe_session_id = VarChar(table, 255, nullable: false),
                amount_cents = table.Column<int>(type: "integer", nullable: false),
                currency = VarChar(table, 3, nullable: false),
                created_at = LegacyTimestamp(table, nullable: false),
                user_id = table.Column<int>(type: "integer", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("donations_pkey", x => x.id);
                table.ForeignKey(
                    name: "fk_cde98962a76ed395",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

    private static void CreateApiUsage(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "api_usage",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                day = table.Column<DateOnly>(type: "date", nullable: false),
                requests = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                api_key_id = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("api_usage_pkey", x => x.id);
                table.ForeignKey(
                    name: "fk_f47e0aa08be312b3",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void CreateBuildVotes(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "build_votes",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                value = table.Column<short>(type: "smallint", nullable: false),
                created_at = LegacyTimestamp(table, nullable: false),
                build_id = table.Column<int>(type: "integer", nullable: false),
                voter_id = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("build_votes_pkey", x => x.id);
                table.ForeignKey(
                    name: "fk_11530dc417c13f8b",
                    column: x => x.build_id,
                    principalTable: "builds",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_11530dc4ebb4b8ad",
                    column: x => x.voter_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        var index = migrationBuilder;
        index.CreateIndex("idx_9579321fa76ed395", "api_keys", "user_id");
        index.CreateIndex("uniq_9579321f57bfb971", "api_keys", "key_hash", unique: true);
        index.CreateIndex("idx_f47e0aa08be312b3", "api_usage", "api_key_id");
        index.CreateIndex("uniq_api_usage_key_day", "api_usage", UsageKeyDay, unique: true);
        index.CreateIndex("idx_11530dc417c13f8b", "build_votes", "build_id");
        index.CreateIndex("idx_11530dc4ebb4b8ad", "build_votes", "voter_id");
        index.CreateIndex(
            "uniq_build_votes_build_voter",
            "build_votes",
            VoteBuildVoter,
            unique: true);
        index.CreateIndex("idx_ab264a57e3c61f9", "builds", "owner_id");
        index.CreateIndex("uniq_ab264a5d6594dd6", "builds", "share_token", unique: true);
        index.CreateIndex("idx_412782017b00651c", "contact_messages", "status");
        index.CreateIndex("idx_412782018b8e8428", "contact_messages", "created_at");
        index.CreateIndex("idx_41278201a76ed395", "contact_messages", "user_id");
        index.CreateIndex("idx_cde98962a76ed395", "donations", "user_id");
        index.CreateIndex("uniq_cde989621a314a57", "donations", "stripe_session_id", unique: true);
        index.CreateIndex("uniq_1483a5e976f5c865", "users", "google_id", unique: true);
    }

    private static OperationBuilder<AddColumnOperation> VarChar(
        ColumnsBuilder table,
        int length,
        bool nullable) =>
        table.Column<string>(
            type: $"character varying({length})",
            maxLength: length,
            nullable: nullable);

    // DEFAULT NULL on a typmod column: PostgreSQL keeps it, pg_dump prints it.
    private static OperationBuilder<AddColumnOperation> NullDefault(
        ColumnsBuilder table,
        int length) =>
        table.Column<string>(
            type: $"character varying({length})",
            maxLength: length,
            nullable: true,
            defaultValueSql: "NULL");

    private static OperationBuilder<AddColumnOperation> Flag(
        ColumnsBuilder table,
        bool defaultValue) =>
        table.Column<bool>(type: "boolean", nullable: false, defaultValue: defaultValue);

    private static OperationBuilder<AddColumnOperation> LegacyTimestamp(
        ColumnsBuilder table,
        bool nullable) =>
        table.Column<DateTime>(type: "timestamp(0) without time zone", nullable: nullable);
}
