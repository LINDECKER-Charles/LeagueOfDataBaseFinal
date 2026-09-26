using System.Text.Json;
using LoDb.Api.Modules.Audit.Import;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Audit.Units;

/// <summary>
/// A line of the legacy journal read as the legacy query service read it: lenient on the
/// optional fields, refused without a time or with an action outside the closed set.
/// </summary>
public sealed class LegacyAuditLineTests
{
    [Fact]
    public void FullLineKeepsEveryField()
    {
        var entry = LegacyAuditLine.Parse(
            """
            {"at":"2026-09-01T10:15:00+02:00","actorType":"user","actorId":"12","actor":"Ahri_Main",
             "action":"apikey.revoke","outcome":"denied","targetType":"apikey","targetId":"k-1",
             "target":"Ma clé","ip":"2001:db8::1","route":"app_apikey_revoke",
             "meta":{"reason":"leak","count":2}}
            """.ReplaceLineEndings(string.Empty));

        Assert.NotNull(entry);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 15, 0, TimeSpan.Zero), entry.OccurredAt);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
        Assert.Equal(
            (AuditActorType.User, 12, "Ahri_Main"),
            (entry.ActorType, entry.ActorId, entry.Actor));
        Assert.Equal(
            (AuditAction.ApiKeyRevoke, AuditOutcome.Denied),
            (entry.Action, entry.Outcome));
        Assert.Equal(
            (AuditTargetType.ApiKey, "k-1", "Ma clé"),
            (entry.TargetType, entry.TargetId, entry.Target));
        Assert.Equal(("2001:db8::1", "app_apikey_revoke"), (entry.Ip, entry.Route));
        Assert.Equal("""{"reason":"leak","count":2}""", entry.Meta);
    }

    [Fact]
    public void MissingOptionalFieldsFallBackAsTheLegacyReaderDid()
    {
        var entry = LegacyAuditLine.Parse(
            """
            {"at":"2026-09-01T10:15:00+00:00","action":"user.logout","outcome":"odd",
             "targetType":"skin","meta":[]}
            """.ReplaceLineEndings(string.Empty));

        Assert.NotNull(entry);
        Assert.Equal(
            (AuditActorType.Anonymous, (int?)null, (string?)null),
            (entry.ActorType, entry.ActorId, entry.Actor));
        Assert.Equal(AuditOutcome.Success, entry.Outcome);
        Assert.Null(entry.TargetType);
        Assert.Null(entry.Meta);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"action":"user.login"}""")]
    [InlineData("""{"at":"yesterday","action":"user.login"}""")]
    [InlineData("""{"at":"2026-09-01T10:15:00+00:00","action":"user.dance"}""")]
    [InlineData("""{"at":"2026-09-01T10:15:00+00:00"}""")]
    public void LineThatIsNoEntryIsRefused(string line) =>
        Assert.Null(LegacyAuditLine.Parse(line));

    [Fact]
    public void LongValuesAreCutToTheirColumns()
    {
        var line = JsonSerializer.Serialize(new
        {
            at = "2026-09-01T10:15:00Z",
            action = "user.login",
            actorType = "user",
            actor = new string('a', 300),
        });

        var entry = LegacyAuditLine.Parse(line);

        Assert.Equal(180, entry?.Actor?.Length);
    }
}
