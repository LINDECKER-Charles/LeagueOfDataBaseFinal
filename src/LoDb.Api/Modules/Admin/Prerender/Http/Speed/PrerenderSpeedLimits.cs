namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>
/// What the environment allows the pace (<c>LoDb:Prerender</c>), the emergency brake of the
/// manual mode, and what a page costs the host, for the admin's predictions.
/// </summary>
internal sealed record PrerenderSpeedLimits
{
    /// <summary>The adaptive floor.</summary>
    public required int MinRendersPerMinute { get; init; }

    /// <summary>The adaptive ceiling.</summary>
    public required int MaxRendersPerMinute { get; init; }

    /// <summary>The hard maximum of the manual target.</summary>
    public required int ManualMaxRendersPerMinute { get; init; }

    /// <summary>The renderers the stack runs (web-ssr-prerender's replicas).</summary>
    public required int Renderers { get; init; }

    /// <summary>The CPU quota of one renderer, in cores.</summary>
    public required decimal RendererCpus { get; init; }

    /// <summary>The brake's threshold on the host's busy share, steal included (0..1).</summary>
    public required decimal BrakeHostBusy { get; init; }

    /// <summary>
    /// Minutes over the threshold, or without the visitors' load, before the brake.
    /// </summary>
    public required int BrakeMinutes { get; init; }

    /// <summary>Minutes the brake holds new renders from its last braking decision.</summary>
    public required int HoldMinutes { get; init; }

    /// <summary>
    /// A renderer's CPU seconds per page: measured over the last 10 minutes when enough pages
    /// were rendered (<see cref="IsCostMeasured"/>), the production figure otherwise.
    /// </summary>
    public required decimal RendererCpuSecondsPerPage { get; init; }

    /// <summary>Whether <see cref="RendererCpuSecondsPerPage"/> was measured here.</summary>
    public required bool IsCostMeasured { get; init; }

    /// <summary>
    /// The API's and Postgres's CPU seconds per page, measured once in production.
    /// </summary>
    public required decimal StackCpuSecondsPerPage { get; init; }
}
