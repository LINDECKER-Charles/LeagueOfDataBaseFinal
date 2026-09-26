using LoDb.Testing;
using Npgsql;

namespace LoDb.Infrastructure.Tests.Persistence.Apps;

/// <summary>
/// <c>client_policy</c> refuses a policy the apps could not follow: an unknown platform, a
/// version that is not three numbers, a partial bundle, or a bundle outside Android.
/// </summary>
public sealed class ClientPolicyConstraintsTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Checksum =
        "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    // The five bundle columns: id, URL, checksum, signature, minimum native version.
    private const string NoBundle = "NULL, NULL, NULL, NULL, NULL";
    private const string Bundle = $"'b1', 'https://x.test/b1', '{Checksum}', 'c2ln', '1.0.0'";
    private const string PartialBundle = "'b1', 'https://x.test/b1', NULL, 'c2ln', '1.0.0'";
    private const string BadChecksumBundle = "'b1', 'https://x.test/b1', 'AB', 'c2ln', '1.0.0'";
    private const string ShortNativeBundle =
        $"'b1', 'https://x.test/b1', '{Checksum}', 'c2ln', '1'";

    public static TheoryData<string> ValidPolicies => new()
    {
        $"'desktop', '1.0.0', '1.2.3', {NoBundle}",
        $"'desktop', NULL, NULL, {NoBundle}",
        $"'android', '1.0.0', '10.20.30', {Bundle}",
    };

    public static TheoryData<string, string> InvalidPolicies => new()
    {
        { $"'ios', NULL, NULL, {NoBundle}", "ck_client_policy_platform" },
        { $"'desktop', '1.2', NULL, {NoBundle}", "ck_client_policy_versions" },
        { $"'desktop', NULL, 'v1.2.3', {NoBundle}", "ck_client_policy_versions" },
        { $"'android', NULL, NULL, {ShortNativeBundle}", "ck_client_policy_versions" },
        { $"'android', NULL, NULL, {PartialBundle}", "ck_client_policy_bundle" },
        { $"'desktop', NULL, NULL, {Bundle}", "ck_client_policy_bundle_platform" },
        { $"'android', NULL, NULL, {BadChecksumBundle}", "ck_client_policy_bundle_checksum" },
    };

    [Theory]
    [MemberData(nameof(ValidPolicies))]
    public async Task ValidPolicyIsKept(string values)
    {
        await InsertAsync(values);

        Assert.Equal(
            ["1"],
            await Database.QueryAsync("SELECT count(*) FROM client_policy", Cancellation));
    }

    [Theory]
    [MemberData(nameof(InvalidPolicies))]
    public async Task InvalidPolicyIsRefused(string values, string constraint)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(values));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task OnePolicyPerPlatform()
    {
        await InsertAsync("'desktop', '1.0.0', NULL, NULL, NULL, NULL, NULL, NULL");

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertAsync("'desktop', '1.1.0', NULL, NULL, NULL, NULL, NULL, NULL"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    private Task InsertAsync(string values) =>
        Database.ExecuteAsync(
            $"""
            INSERT INTO client_policy (platform, minimum_version, latest_version, bundle_id,
                bundle_url, bundle_checksum, bundle_signature, bundle_minimum_native_version,
                published_at)
            VALUES ({values}, now())
            """,
            Cancellation);
}
