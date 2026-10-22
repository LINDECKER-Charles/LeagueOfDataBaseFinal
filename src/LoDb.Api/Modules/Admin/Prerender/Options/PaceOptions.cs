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

    /// <summary>
    /// The hard maximum of the manual target in production: two renderers at a core each, at
    /// about 0.1 s of CPU a page (measured 2026-10-21), with half of each core spare.
    /// </summary>
    public const int DefaultManualMaxRendersPerMinute = 600;

    /// <summary>The CPU quota of one renderer on a host (compose.deploy.yaml).</summary>
    public const double DefaultRendererCpus = 1.0;

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

    /// <summary>
    /// The most the operator may ask in manual mode (the admin's speed setting): also the
    /// budget of each renderer's lane (<c>LODB_PRERENDER_RENDERS_PER_MINUTE</c> of
    /// web-ssr-prerender), which bounds the pass whatever it asks: kept at the ceiling or above,
    /// or the lane refuses the adaptive pass's renders over it.
    /// </summary>
    public int ManualMaxRendersPerMinute { get; set; } = DefaultManualMaxRendersPerMinute;

    /// <summary>
    /// The CPU quota of one renderer (<c>cpus</c> of web-ssr-prerender): what the admin's
    /// resources view takes as a renderer's capacity. Read, never enforced here.
    /// </summary>
    public double RendererCpus { get; set; } = DefaultRendererCpus;

    public bool IsAdaptive => Mode == PaceMode.Adaptive;
}
