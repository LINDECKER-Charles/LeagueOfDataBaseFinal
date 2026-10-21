namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>What the sampler reads of the host and the API itself, beside the SSRs' load.</summary>
/// <param name="HostCpus">The host's CPUs; null when unknown.</param>
/// <param name="ApiCores">Cores the API used over the last minute; null when unknown.</param>
internal sealed record StackCpu(int? HostCpus, double? ApiCores);
