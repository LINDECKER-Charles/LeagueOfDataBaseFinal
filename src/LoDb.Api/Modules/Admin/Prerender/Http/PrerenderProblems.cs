using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Http;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// The refusals of the pre-rendering's admin API, as ProblemDetails whose <c>code</c> the
/// panel switches on.
/// </summary>
internal static class PrerenderProblems
{
    public const string PassNotFound = "prerender-pass-not-found";

    /// <summary>409: a pass is already running or paused; one at a time.</summary>
    public const string PassActive = "prerender-pass-active";

    /// <summary>
    /// 409: the pass cannot go there from where it is (a finished pass, a pass already
    /// paused…), or it moved meanwhile.
    /// </summary>
    public const string PassState = "prerender-pass-state";

    /// <summary>
    /// 409: the pass or its store is not configured on this host: a pass would never run.
    /// </summary>
    public const string Dormant = "prerender-dormant";

    public static AccountProblem NotFound() => AdminProblems.NotFound(PassNotFound);

    public static AccountProblem Conflict(string code) => AdminProblems.Conflict(code);
}
