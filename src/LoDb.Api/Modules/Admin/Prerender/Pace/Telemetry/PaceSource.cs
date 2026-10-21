namespace LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;

/// <summary>What the regulator reads, each said lost and restored once per outage.</summary>
internal enum PaceSource
{
    /// <summary>The host's <c>/proc/stat</c>.</summary>
    HostCpu,

    /// <summary>The API's own <c>cpu.stat</c>.</summary>
    ApiCgroup,

    /// <summary>The visitors' SSR <c>/load</c>.</summary>
    VisitorsSsr,

    /// <summary>The pass's SSR <c>/load</c>.</summary>
    PassSsr,
}
