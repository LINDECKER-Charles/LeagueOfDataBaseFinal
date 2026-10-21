using LoDb.Infrastructure.Audit;

namespace LoDb.Infrastructure.Tests.Audit;

/// <summary>
/// The closed sets of the journal: the 24 actions of the legacy stack under their legacy
/// names, then those of the new stack, with stable numbers, and the stored spelling of every
/// other value.
/// </summary>
public sealed class AuditVocabularyTests
{
    // app/src/Service/Audit/Model/AuditAction.php, in its order.
    private static readonly string[] LegacyActions =
    [
        "user.login", "user.login_failed", "user.logout", "user.register",
        "user.password_reset", "user.email_verified", "profile.update", "account.delete",
        "build.create", "build.update", "build.delete", "build.vote",
        "apikey.create", "apikey.regenerate", "apikey.revoke",
        "admin.user_ban", "admin.user_unban", "admin.user_delete", "admin.build_hide",
        "admin.build_delete", "admin.api_client_revoke", "admin.api_client_credit",
        "admin.analytics_rollup", "admin.logs_purge",
    ];

    // The actions the new stack added, numbered after the legacy ones, in their order.
    private static readonly string[] NewActions =
    [
        "admin.prerender_start", "admin.prerender_pause", "admin.prerender_resume",
        "admin.prerender_cancel",
    ];

    private static readonly string[] AllActions = [.. LegacyActions, .. NewActions];

    [Fact]
    public void ActionsAreTheLegacyOnesUnderTheirLegacyNamesThenTheNewOnes() =>
        Assert.Equal(
            AllActions,
            Enum.GetValues<AuditAction>().Select(static action => AuditVocabulary.ToText(action)));

    // The numbers are the event ids of the log mirror: dashboards and alerts may use them.
    [Fact]
    public void ActionNumbersRunFromOneInTheLegacyOrderThenTheNewOrder() =>
        Assert.Equal(
            Enumerable.Range(1, AllActions.Length),
            Enum.GetValues<AuditAction>().Select(static action => (int)action));

    [Fact]
    public void EveryStoredValueReadsBack()
    {
        Assert.All(Enum.GetValues<AuditAction>(), static value =>
            Assert.Equal(value, AuditVocabulary.ParseAction(AuditVocabulary.ToText(value))));
        Assert.All(Enum.GetValues<AuditOutcome>(), static value =>
            Assert.Equal(value, AuditVocabulary.ParseOutcome(AuditVocabulary.ToText(value))));
        Assert.All(Enum.GetValues<AuditActorType>(), static value =>
            Assert.Equal(value, AuditVocabulary.ParseActorType(AuditVocabulary.ToText(value))));
        Assert.All(Enum.GetValues<AuditTargetType>(), static value =>
            Assert.Equal(value, AuditVocabulary.ParseTargetType(AuditVocabulary.ToText(value))));
    }

    // The same words as the legacy journal and the checks of audit_log.
    [Fact]
    public void OtherValuesAreSpelledAsTheLegacyJournal()
    {
        Assert.Equal(
            ["success", "failure", "denied"],
            Enum.GetValues<AuditOutcome>().Select(static value => AuditVocabulary.ToText(value)));
        Assert.Equal(
            ["user", "admin", "anonymous"],
            Enum.GetValues<AuditActorType>().Select(static value => AuditVocabulary.ToText(value)));
        Assert.Equal(
            ["user", "build", "apikey", "api_client", "prerender_pass"],
            Enum.GetValues<AuditTargetType>()
                .Select(static value => AuditVocabulary.ToText(value)));
    }

    [Fact]
    public void ValuesOutsideTheSetsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AuditVocabulary.ToText((AuditAction)(AllActions.Length + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AuditVocabulary.ParseAction("user.impersonate"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AuditVocabulary.ParseOutcome("Success"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AuditVocabulary.ParseActorType("system"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AuditVocabulary.ParseTargetType("comment"));
    }
}
