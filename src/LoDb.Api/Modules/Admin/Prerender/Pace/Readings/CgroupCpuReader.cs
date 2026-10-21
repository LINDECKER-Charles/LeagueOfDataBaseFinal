using System.Globalization;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>The API container's own <c>cpu.stat</c>, under cgroup v2.</summary>
/// <remarks>
/// <c>throttled_usec</c> is only written when the CPU controller enforces a quota: without
/// one, nothing throttles the container, and it reads as zero.
/// </remarks>
internal sealed class CgroupCpuReader : ICgroupCpuReader
{
    public const string CpuStatPath = "/sys/fs/cgroup/cpu.stat";

    private const string UsageKey = "usage_usec";
    private const string ThrottledKey = "throttled_usec";

    public async Task<CgroupCpuSample?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Parse(await File.ReadAllTextAsync(CpuStatPath, cancellationToken));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The sample a <c>cpu.stat</c> text gives; null without its usage.</summary>
    public static CgroupCpuSample? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var values = text.Split('\n')
            .Select(static line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(static fields => fields.Length == 2)
            .Select(static fields => (Key: fields[0], Value: ValueOf(fields[1])))
            .Where(static field => field.Value is not null)
            .GroupBy(static field => field.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.First().Value!.Value,
                StringComparer.Ordinal);
        return values.TryGetValue(UsageKey, out var usage)
            ? new CgroupCpuSample(usage, values.GetValueOrDefault(ThrottledKey))
            : null;
    }

    private static long? ValueOf(string text) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
