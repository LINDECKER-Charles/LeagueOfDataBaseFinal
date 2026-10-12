using System.Globalization;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// One <see cref="BillingApp"/> for the tests of a class: each seeds accounts of its own,
/// delivers Stripe identifiers of its own (<see cref="Payloads"/>), and starts with the
/// doubles emptied.
/// </summary>
public sealed class SharedBillingApp(PostgresContainerFixture postgres)
    : SharedApp<BillingApp>(postgres)
{
    /// <summary>The payloads of a test, marked as those of no other test of the host.</summary>
    public StripePayloads Payloads() =>
        new(string.Create(CultureInfo.InvariantCulture, $"_{NextNumber()}"));

    protected override Task<BillingApp> StartAsync(PostgresContainerFixture server) =>
        BillingApp.StartSharedAsync(server);
}
