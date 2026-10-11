using LoDb.Api.Modules.Accounts.Links;
using LoDb.Api.Modules.Accounts.Security.Limits;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Donations;
using LoDb.Api.Modules.Billing.Expiry;
using LoDb.Api.Modules.Billing.Fulfilment;
using LoDb.Api.Modules.Billing.Http;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Api.Modules.Billing.Payments;
using LoDb.Api.Modules.Billing.Purchases;
using LoDb.Api.Modules.Billing.Revocation;
using LoDb.Api.Modules.Billing.Subscriptions;
using LoDb.Api.Modules.Billing.Webhooks;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Billing;

/// <summary>
/// Billing module: Stripe checkout, idempotent webhooks, credits and donations.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The daily expiry of the
/// credits runs as <c>Workers/Billing/CreditExpiryJob</c>.
/// </remarks>
internal static class BillingModule
{
    public static IServiceCollection AddBilling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BillingOptions>()
            .Bind(configuration.GetSection(BillingOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<BillingEffects>();
        services.TryAddScoped<CreditExpiry>();
        AddCheckout(services);
        AddWebhook(services);
        return services;
    }

    public static IEndpointRouteBuilder MapBilling(this IEndpointRouteBuilder endpoints)
    {
        DonationOptionsEndpoint.Map(endpoints);
        DonationCheckoutEndpoint.Map(endpoints);
        OffersEndpoint.Map(endpoints);
        PackCheckoutEndpoint.Map(endpoints);
        PlanCheckoutEndpoint.Map(endpoints);
        StripeWebhookEndpoint.Map(endpoints);
        return endpoints;
    }

    private static void AddCheckout(IServiceCollection services)
    {
        services.AddHttpClient(StripeCheckoutGateway.HttpClientName);
        services.AddHttpContextAccessor();
        services.TryAddSingleton<ICheckoutGateway, StripeCheckoutGateway>();
        services.TryAddScoped<LinkOrigin>();
        services.TryAddScoped<CheckoutOpener>();
        services.TryAddScoped<BillingAccounts>();
        services.TryAddScoped<DonationOptionsEndpoint>();
        services.TryAddScoped<DonationCheckoutEndpoint>();
        services.TryAddScoped<OffersEndpoint>();
        services.TryAddScoped<PackCheckoutEndpoint>();
        services.TryAddScoped<PlanCheckoutEndpoint>();
        services.AddAccountActionLimit(PurchaseLimits.Checkout);
    }

    private static void AddWebhook(IServiceCollection services)
    {
        services.TryAddScoped<WebhookSignature>();
        services.TryAddScoped<WebhookProcessor>();
        services.TryAddScoped<StripeWebhookEndpoint>();
        services.TryAddScoped<CheckoutLedger>();
        services.TryAddScoped<SessionEventReader>();
        services.TryAddScoped<SessionFulfilment>();
        services.TryAddScoped<BuyerKeys>();
        services.TryAddScoped<PackFulfilment>();
        services.TryAddScoped<PlanFulfilment>();
        services.TryAddScoped<DonationFulfilment>();
        services.TryAddScoped<SubscriptionEntitlements>();
        services.TryAddScoped<PaymentRevocation>();
        services.TryAddScoped<CreditRevocation>();
        services.TryAddScoped<DonationRevocation>();
        AddHandlers(services);
    }

    // One handler per type of event, which the processor picks by its type. The Stripe
    // endpoint must send each of these types (docs/guides/configuration.md, § 6.5).
    private static void AddHandlers(IServiceCollection services)
    {
        AddHandler<CheckoutCompletedHandler>(services);
        AddHandler<AsyncPaymentSucceededHandler>(services);
        AddHandler<AsyncPaymentFailedHandler>(services);
        AddHandler<SubscriptionUpdatedHandler>(services);
        AddHandler<SubscriptionDeletedHandler>(services);
        AddHandler<InvoicePaymentFailedHandler>(services);
        AddHandler<ChargeRefundedHandler>(services);
        AddHandler<DisputeCreatedHandler>(services);
    }

    private static void AddHandler<THandler>(IServiceCollection services)
        where THandler : class, IStripeEventHandler =>
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStripeEventHandler, THandler>());
}
