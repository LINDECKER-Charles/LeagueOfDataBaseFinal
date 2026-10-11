using Microsoft.EntityFrameworkCore.Migrations;

namespace LoDb.Infrastructure.Persistence.Migrations;

/// <summary>
/// What became of each payment: the Checkout sessions the webhook received
/// (<c>stripe_checkout</c>: pending, paid, failed, refused or revoked) and the plan each
/// subscription pays for, with whether its key holds it (<c>stripe_subscription</c>).
/// </summary>
/// <remarks>
/// Additive: two new tables, which the legacy stack ignores. They start empty: a session paid
/// before them keeps its grant or donation, unique by session, and a subscription sold before
/// them gets its row from its key the first time an event about it arrives.
/// </remarks>
public partial class StripePaymentStates : Migration
{
    private const string Checkout = "stripe_checkout";
    private const string Subscription = "stripe_subscription";
    private const string StripeId = "character varying(255)";
    private const string Code = "character varying(16)";
    private const string Instant = "timestamp with time zone";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateCheckout(migrationBuilder);
        CreateSubscription(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: Checkout);
        migrationBuilder.DropTable(name: Subscription);
    }

    private static void CreateCheckout(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: Checkout,
            columns: table => new
            {
                id = table.Column<string>(type: StripeId, maxLength: 255, nullable: false),
                kind = table.Column<string>(type: Code, maxLength: 16, nullable: false),
                status = table.Column<string>(type: Code, maxLength: 16, nullable: false),
                payment_intent_id = table.Column<string>(
                    type: StripeId,
                    maxLength: 255,
                    nullable: true),
                amount_cents = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(
                    type: "character varying(3)",
                    maxLength: 3,
                    nullable: false),
                created_at = table.Column<DateTimeOffset>(type: Instant, nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: Instant, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stripe_checkout", x => x.id);
                table.CheckConstraint(
                    "ck_stripe_checkout_kind",
                    "kind IN ('api_pack', 'api_plan', 'donation')");
                table.CheckConstraint(
                    "ck_stripe_checkout_status",
                    "status IN ('pending', 'paid', 'failed', 'refused', 'revoked')");
            });
        migrationBuilder.CreateIndex(
            name: "ix_stripe_checkout_payment_intent_id",
            table: Checkout,
            column: "payment_intent_id",
            unique: true,
            filter: "payment_intent_id IS NOT NULL");
    }

    private static void CreateSubscription(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: Subscription,
            columns: table => new
            {
                id = table.Column<string>(type: StripeId, maxLength: 255, nullable: false),
                plan = table.Column<string>(type: Code, maxLength: 16, nullable: false),
                status = table.Column<string>(type: Code, maxLength: 16, nullable: false),
                changed_at = table.Column<DateTimeOffset>(type: Instant, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stripe_subscription", x => x.id);
                table.CheckConstraint(
                    "ck_stripe_subscription_status",
                    "status IN ('entitled', 'suspended', 'ended')");
            });
}
