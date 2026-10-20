using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Infrastructure.Persistence.Prerender;

namespace LoDb.Api.Modules.Admin.Prerender.Http.History;

/// <summary>The view of a pass at the time of the report: a pure function of its facts.</summary>
internal static class PassViews
{
    public static PrerenderPassView Of(PassFacts facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var pass = facts.Pass;
        var activeSeconds = PassProgress.ActiveSeconds(pass, now);
        var rate = PassProgress.PagesPerMinute(pass, activeSeconds);
        return new PrerenderPassView
        {
            Id = pass.Id,
            Epoch = pass.Epoch,
            State = PrerenderColumns.ToText(pass.State),
            Scope = ScopeOf(PassScopeJson.Read(pass.Scope)),
            AuthorId = pass.AuthorId,
            AuthorName = facts.AuthorName,
            CreatedAt = pass.CreatedAt,
            FinishedAt = pass.FinishedAt,
            ActiveSeconds = activeSeconds,
            PagesTotal = pass.PagesTotal,
            PagesWritten = pass.PagesWritten,
            PagesSkipped = pass.PagesSkipped,
            PagesFailed = pass.PagesFailed,
            BytesWritten = pass.BytesWritten,
            Units = UnitsOf(facts),
            PagesPerMinute = PassProgress.Shown(rate),
            EstimatedEndAt = PassProgress.EstimatedEnd(facts, rate, now),
            // TODO(B2): PausedAt and PauseReason from prerender_pass.paused_at and
            // pause_reason, once the PrerenderDiagnostics migration adds them.
            PagesLeft = PassProgress.PagesLeft(facts),
        };
    }

    // The versions are counted, not listed: a report carries 50 passes, read every minute.
    private static PrerenderScopeView ScopeOf(PassScope scope) => new()
    {
        VersionCount = scope.Versions?.Count,
        NewestListed = scope.Versions?.Max()?.Value,
        OldestListed = scope.Versions?.Min()?.Value,
        Oldest = scope.Oldest?.Value,
        Newest = scope.Newest?.Value,
        Locales = scope.Locales?.Select(UiLocales.Code).ToList(),
        PageTypes = scope.PageTypes?.Select(CanonicalPath.SegmentOf).ToList(),
        Priority = [.. scope.Priority.Select(static version => version.Value)],
        SkipComplete = scope.SkipComplete,
    };

    private static PrerenderUnitCounts UnitsOf(PassFacts facts) => new()
    {
        Total = facts.Units.Values.Sum(),
        Pending = facts.UnitsIn(PrerenderUnitState.Pending),
        Running = facts.UnitsIn(PrerenderUnitState.Running),
        Completed = facts.UnitsIn(PrerenderUnitState.Completed),
        Failed = facts.UnitsIn(PrerenderUnitState.Failed),
    };
}
