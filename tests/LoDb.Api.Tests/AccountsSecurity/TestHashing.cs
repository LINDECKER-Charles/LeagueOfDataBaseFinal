using LoDb.Api.Modules.Accounts.Security.Hashing;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// The password hashing of the API built without a host, for the tests that only hash: one
/// gate for the whole run, as the host holds one.
/// </summary>
internal static class TestHashing
{
    private static readonly SecurityMeters Meters = new();

    public static HashingGate Gate { get; } = new(Meters.Metrics);

    public static MigratingPasswordHasher Hasher() => new(new Argon2Passwords(Gate), Gate);
}
