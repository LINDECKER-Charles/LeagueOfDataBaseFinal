using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Tests.Persistence;
using LoDb.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Infrastructure.Tests.Audit;

/// <summary>
/// Each recorded event has a copy in the logs, <c>audit.&lt;action&gt;</c>, built from an
/// allow-list: no address, no label, no <c>meta.identifier</c>.
/// </summary>
public sealed class AuditMirrorTests(PostgresContainerFixture postgres)
    : MigratedDatabase(postgres)
{
    private const string Identifier = "alice@example.test";

    [Fact]
    public async Task SuccessIsCopiedAtInformationUnderTheActionName()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);
        harness.Requests.HttpContext = AuditHarness.Request(AuditHarness.SignedIn(3, "alice"));

        await harness.Journal.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.BuildVote,
                Target = new AuditTarget(AuditTargetType.Build, "42", "Mid Ahri"),
                Meta = new Dictionary<string, object?> { ["value"] = 1 },
            },
            Cancellation);

        var copy = Assert.Single(harness.Records);
        Assert.Equal(
            ("audit.build.vote", (int)AuditAction.BuildVote, LogLevel.Information),
            (copy.Id.Name, copy.Id.Id, copy.Level));
        Assert.Equal("Audit build.vote: success.", copy.Message);
        Assert.Equal(
            [
                "ActorType=user", "ActorId=3", "Outcome=success", "TargetType=build",
                "TargetId=42", $"Route={AuditHarness.Route}", "Meta.value=1",
            ],
            Fields(copy));
    }

    [Fact]
    public async Task CopyLeavesOutTheAddressTheLabelsAndTheIdentifier()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);
        harness.Requests.HttpContext = AuditHarness.Request(new());

        await harness.Journal.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.UserLoginFailed,
                Outcome = AuditOutcome.Failure,
                Actor = AuditActor.User(3, "alice"),
                Target = new AuditTarget(AuditTargetType.User, "3", "alice"),
                Meta = new Dictionary<string, object?>
                {
                    ["identifier"] = Identifier,
                    ["reason"] = "bad_credentials",
                },
            },
            Cancellation);

        var copy = Assert.Single(harness.Records);
        Assert.Equal(
            ("audit.user.login_failed", LogLevel.Warning),
            (copy.Id.Name, copy.Level));
        Assert.Equal(
            [
                "ActorType=user", "ActorId=3", "Outcome=failure", "TargetType=user",
                "TargetId=3", $"Route={AuditHarness.Route}", "Meta.reason=bad_credentials",
            ],
            Fields(copy));
        Assert.DoesNotContain("alice", string.Join('\n', Fields(copy)), StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.9", copy.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeniedActionIsCopiedAtWarning()
    {
        await using var harness = new AuditHarness(Database.ConnectionString);

        await harness.Journal.RecordAsync(
            new AuditEvent { Action = AuditAction.AdminUserDelete, Outcome = AuditOutcome.Denied },
            Cancellation);

        var copy = Assert.Single(harness.Records);
        Assert.Equal(
            ("audit.admin.user_delete", LogLevel.Warning, "Audit admin.user_delete: denied."),
            (copy.Id.Name, copy.Level, copy.Message));
        Assert.Equal(["ActorType=anonymous", "Outcome=denied"], Fields(copy));
    }

    private static string[] Fields(FakeLogRecord record) =>
    [
        .. (record.StructuredState ?? [])
            .Select(static field => $"{field.Key}={field.Value}"),
    ];
}
