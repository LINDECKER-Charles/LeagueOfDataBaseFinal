using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Apps;

internal sealed class ClientPolicyEntryConfiguration : IEntityTypeConfiguration<ClientPolicyEntry>
{
    private const string ReleaseNumber = "'^[0-9]+\\.[0-9]+\\.[0-9]+$'";

    private static readonly ValueConverter<AppPlatform, string> PlatformConverter = new(
        platform => ToText(platform),
        value => ParsePlatform(value));

    public void Configure(EntityTypeBuilder<ClientPolicyEntry> builder)
    {
        builder.ToTable("client_policy", static table =>
        {
            table.HasCheckConstraint(
                "ck_client_policy_platform",
                "platform IN ('desktop', 'android')");
            table.HasCheckConstraint(
                "ck_client_policy_versions",
                $"minimum_version ~ {ReleaseNumber} AND latest_version ~ {ReleaseNumber}"
                + $" AND bundle_minimum_native_version ~ {ReleaseNumber}");
            table.HasCheckConstraint(
                "ck_client_policy_bundle",
                "num_nonnulls(bundle_id, bundle_url, bundle_checksum, bundle_signature,"
                + " bundle_minimum_native_version) IN (0, 5)");
            table.HasCheckConstraint(
                "ck_client_policy_bundle_platform",
                "platform = 'android' OR bundle_id IS NULL");
            table.HasCheckConstraint(
                "ck_client_policy_bundle_checksum",
                "bundle_checksum ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(policy => policy.Platform);

        builder.Property(policy => policy.Platform)
            .HasMaxLength(16)
            .HasConversion(PlatformConverter);
        builder.Property(policy => policy.MinimumVersion)
            .HasMaxLength(ClientPolicyEntry.VersionMaxLength);
        builder.Property(policy => policy.LatestVersion)
            .HasMaxLength(ClientPolicyEntry.VersionMaxLength);
        builder.Property(policy => policy.BundleId)
            .HasMaxLength(ClientPolicyEntry.BundleIdMaxLength);
        builder.Property(policy => policy.BundleUrl)
            .HasMaxLength(ClientPolicyEntry.BundleUrlMaxLength);
        builder.Property(policy => policy.BundleChecksum).HasMaxLength(64);
        builder.Property(policy => policy.BundleSignature)
            .HasMaxLength(ClientPolicyEntry.BundleSignatureMaxLength);
        builder.Property(policy => policy.BundleMinimumNativeVersion)
            .HasMaxLength(ClientPolicyEntry.VersionMaxLength);
    }

    private static string ToText(AppPlatform platform) => platform switch
    {
        AppPlatform.Desktop => "desktop",
        AppPlatform.Android => "android",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
    };

    private static AppPlatform ParsePlatform(string value) => value switch
    {
        "desktop" => AppPlatform.Desktop,
        "android" => AppPlatform.Android,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
