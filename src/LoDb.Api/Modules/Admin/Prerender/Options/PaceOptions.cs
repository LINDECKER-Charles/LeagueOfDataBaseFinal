namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>
/// The adaptive pace of the pass (<c>LoDb:Prerender:Pace</c>): the floor and the ceiling of the
/// renders it starts per minute, the step it climbs by, and where it reads the SSRs' load
/// (docs/produit/pre-rendu-catalogue-cdn.md, lot 2, « Capacité adaptative de la passe »).
/// </summary>
internal sealed class PaceOptions
{
    /// <summary>The floor in production: about 2 % of the VPS.</summary>
    public const int DefaultMinRendersPerMinute = 30;

    /// <summary>
    /// The ceiling in production: the pass's SSR stays under half a core, with a wide margin on
    /// the host's free CPU.
    /// </summary>
    public const int DefaultMaxRendersPerMinute = 240;

    /// <summary>The step in production: from the floor to the ceiling in 7 minutes.</summary>
    public const int DefaultStepRendersPerMinute = 30;

    /// <summary>Adaptive by default; <see cref="PaceMode.Fixed"/> is the kill switch.</summary>
    public PaceMode Mode { get; set; } = PaceMode.Adaptive;

    /// <summary>The floor, and the pace each drain of a pass starts at.</summary>
    public int MinRendersPerMinute { get; set; } = DefaultMinRendersPerMinute;

    /// <summary>
    /// The ceiling: also the budget of the pass's SSR lane (its
    /// <c>LODB_PRERENDER_RENDERS_PER_MINUTE</c>), which bounds the pass whatever it asks.
    /// </summary>
    public int MaxRendersPerMinute { get; set; } = DefaultMaxRendersPerMinute;

    /// <summary>What one increase adds to the pace.</summary>
    public int StepRendersPerMinute { get; set; } = DefaultStepRendersPerMinute;

    /// <summary>
    /// The visitors' SSR load (<c>http://lodb-web-ssr:9464/load</c>); unset, its signals read
    /// as missing and the pace stays at the floor.
    /// </summary>
    public string? VisitorsLoadUrl { get; set; }

    /// <summary>The pass's SSR load (<c>http://lodb-web-ssr-prerender:9464/load</c>).</summary>
    public string? PassLoadUrl { get; set; }

    /// <summary>
    /// The CPUs of <c>/proc/stat</c> the host's shares count (<c>0-3</c>, <c>0,2-3</c>), for a
    /// bench that pins the stack on a few CPUs of a larger VM. Never set on a host: unset, the
    /// aggregate line counts every CPU.
    /// </summary>
    public string? HostCpuSet { get; set; }

    public bool IsAdaptive => Mode == PaceMode.Adaptive;
}
