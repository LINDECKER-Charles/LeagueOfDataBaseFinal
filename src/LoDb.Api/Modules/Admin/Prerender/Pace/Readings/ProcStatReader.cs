using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Options;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>
/// The host's CPU time from <c>/proc/stat</c>, which a container reads as the VM's own (no
/// lxcfs on the hosts: docs/guides/configuration.md, § 4.10).
/// </summary>
/// <remarks>
/// The total is user, nice, system, idle, iowait, irq, softirq and steal. Guest and guest_nice
/// are already counted in user and nice: adding them would count a guest's time twice.
/// </remarks>
internal sealed class ProcStatReader(IOptions<PrerenderOptions> options) : IHostCpuReader
{
    public const string ProcStatPath = "/proc/stat";

    private const string AggregateLabel = "cpu";
    private const int TotalFields = 8;
    private const int IdleField = 3;
    private const int StealField = 7;

    public async Task<HostCpuSample?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(ProcStatPath, cancellationToken);
            return Parse(text, CpuSet.Parse(options.Value.Pace.HostCpuSet));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The aggregate line of <paramref name="text"/>, or the sum of the lines of
    /// <paramref name="cpus"/> when set; null when a line is missing or malformed.
    /// </summary>
    public static HostCpuSample? Parse(string text, IReadOnlySet<int>? cpus)
    {
        ArgumentNullException.ThrowIfNull(text);
        var wanted = cpus?
            .Select(static cpu => AggregateLabel + cpu.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal) ?? [AggregateLabel];
        var samples = text.Split('\n')
            .Select(static line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length > 0 && wanted.Contains(fields[0]))
            .Select(static fields => SampleOf(fields))
            .ToList();
        if (samples.Count != wanted.Count || samples.Any(static sample => sample is null))
        {
            return null;
        }

        return new HostCpuSample(
            samples.Sum(static sample => sample!.Idle),
            samples.Sum(static sample => sample!.Steal),
            samples.Sum(static sample => sample!.Total));
    }

    private static HostCpuSample? SampleOf(string[] fields)
    {
        var values = new long[TotalFields];
        for (var index = 0; index < TotalFields; index++)
        {
            if (index + 1 >= fields.Length || !TryTicks(fields[index + 1], out values[index]))
            {
                return null;
            }
        }

        return new HostCpuSample(values[IdleField], values[StealField], values.Sum());
    }

    private static bool TryTicks(string text, out long ticks) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ticks);
}
