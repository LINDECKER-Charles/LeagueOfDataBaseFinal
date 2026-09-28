using LoDb.Api.Modules.Admin.Http;

namespace LoDb.Api.Modules.Admin.Donations;

/// <summary>A donation, an immutable line written by the Stripe webhook.</summary>
internal sealed record AdminDonationRow
{
    public required int Id { get; init; }

    public required int AmountCents { get; init; }

    /// <summary>ISO 4217 code, lowercase as Stripe writes it.</summary>
    public required string Currency { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The account signed in when giving; null for an anonymous gift.</summary>
    public AdminUserRef? Donor { get; init; }

    /// <summary>Whether the donor's account is marked supporter now.</summary>
    public required bool DonorIsSupporter { get; init; }
}
