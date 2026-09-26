using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoDb.Api.Hosting.Health;

/// <summary>
/// Readiness of the storage: a file can be created, written and flushed in
/// <c>LoDb:Storage:Root</c>.
/// </summary>
/// <remarks>
/// The probe file is unique and deleted on close, so concurrent probes never collide and
/// nothing is left behind. The check creates no directory: a missing root is a deployment
/// error that readiness must reveal.
/// </remarks>
internal sealed class StorageReadinessCheck(IConfiguration configuration) : IHealthCheck
{
    public const string RootKey = "LoDb:Storage:Root";
    private const string MissingRoot = "LoDb:Storage:Root is not set.";
    private const string ProbePrefix = ".readyz-";
    private const int BufferSize = 1;

    private static readonly ReadOnlyMemory<byte> ProbeContent = new byte[] { 1 };

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var root = configuration[RootKey];
        if (string.IsNullOrWhiteSpace(root))
        {
            return HealthCheckResult.Unhealthy(MissingRoot);
        }

        var path = Path.Combine(root, ProbePrefix + Guid.NewGuid().ToString("N"));
        await using var probe = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await probe.WriteAsync(ProbeContent, cancellationToken);
        await probe.FlushAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
