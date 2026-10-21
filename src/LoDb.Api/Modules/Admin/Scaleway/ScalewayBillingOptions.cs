namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// The read-only billing key of Scaleway and the projects whose Object Storage bill the admin
/// panels show (<c>LoDb:ScalewayBilling</c>), checked when the host starts.
/// </summary>
/// <remarks>
/// Not <c>LoDb:Billing</c>, the donations' Stripe settings. While <see cref="SecretKey"/> is
/// empty or blank the bill is off and the panels say so; once it is set, the three ids are
/// required. The key belongs to an IAM application holding <c>BillingReadOnly</c> on the
/// organization and nothing else: it reads the bill, never a bucket.
/// </remarks>
internal sealed class ScalewayBillingOptions
{
    public const string SectionName = "LoDb:ScalewayBilling";

    /// <summary>Secret key of the billing reader, sent as <c>X-Auth-Token</c>.</summary>
    public string? SecretKey { get; set; }

    /// <summary>The organization the bill is read for, a UUID.</summary>
    public string? OrganizationId { get; set; }

    /// <summary>The project of the backups bucket, a UUID.</summary>
    public string? BackupsProjectId { get; set; }

    /// <summary>The project of this environment's page store, a UUID.</summary>
    public string? PagesProjectId { get; set; }

    /// <summary>Whether the bill is read at all.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
