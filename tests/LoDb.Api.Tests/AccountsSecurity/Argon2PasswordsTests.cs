using Isopoh.Cryptography.Argon2;
using LoDb.Api.Modules.Accounts.Security.Hashing;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// What an argon2 hash made elsewhere is worth: below the target it is rewritten, above it
/// it is kept; argon2d, which PHP does not read, fails, and so does a memory cost above PHP's
/// default, before any computation. Two computations at most run at once, and the gauge of
/// the computations in flight says so.
/// </summary>
public sealed class Argon2PasswordsTests
{
    private const string Password = "Épée légère — 2026";

    // PHP's default memory cost, the most a hash of the site was made with.
    private const int PhpDefaultMemoryKib = 65_536;

    private const int Computations = 3;
    private const string InFlightGauge = "lodb.hashing.in_flight";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMilliseconds(200);

    private static readonly User Anyone = new() { Roles = [] };

    // Changes to the parameters of the target, named for the display of the theory. The
    // target itself, a stronger hash, argon2i, two lanes and less memory are PHP's own hashes
    // (PhpHashesTests) or the target's (MigratingPasswordHasherTests).
    private static readonly Dictionary<string, Action<Argon2Config>> Changes = new()
    {
        ["version 16"] = static config => config.Version = Argon2Version.Sixteen,
        ["one pass"] = static config => config.TimeCost = 1,
        ["argon2d"] = static config => config.Type = Argon2Type.DataDependentAddressing,
        ["more memory than PHP's default"] = static config =>
            (config.MemoryCost, config.TimeCost) = (PhpDefaultMemoryKib + 1, 1),
    };

    public static TheoryData<string, PasswordVerificationResult> Verdicts => new()
    {
        { "version 16", PasswordVerificationResult.SuccessRehashNeeded },
        { "one pass", PasswordVerificationResult.SuccessRehashNeeded },
        { "argon2d", PasswordVerificationResult.Failed },
        { "more memory than PHP's default", PasswordVerificationResult.Failed },
    };

    [Theory]
    [MemberData(nameof(Verdicts))]
    public void HashMadeElsewhereIsKeptRewrittenOrRefused(
        string change,
        PasswordVerificationResult verdict) =>
        Assert.Equal(
            verdict,
            TestHashing.Hasher().VerifyHashedPassword(
                Anyone,
                Argon2Hashes.Make(Password, Changes[change]),
                Password));

    [Fact]
    public async Task AtMostTwoComputationsRunAtOnce()
    {
        using var meters = new SecurityMeters();
        using var inFlight = meters.Collect<int>(InFlightGauge);
        using var gate = new HashingGate(meters.Metrics);
        using var release = new ManualResetEventSlim();
        var running = 0;

        var computations = Enumerable.Range(0, Computations)
            .Select(_ => Task.Run(
                () => gate.Run(() =>
                {
                    Interlocked.Increment(ref running);
                    release.Wait(TestContext.Current.CancellationToken);
                    return Interlocked.Decrement(ref running);
                }),
                TestContext.Current.CancellationToken))
            .ToArray();

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref running) == 2, Patience));
        // Time for the third one to get in, had the gate let it.
        await Task.Delay(GracePeriod, TestContext.Current.CancellationToken);
        Assert.Equal(2, Volatile.Read(ref running));
        inFlight.RecordObservableInstruments();
        Assert.Equal(2, inFlight.LastMeasurement?.Value);
        release.Set();
        await Task.WhenAll(computations);
        inFlight.RecordObservableInstruments();
        Assert.Equal(0, inFlight.LastMeasurement?.Value);
    }
}
