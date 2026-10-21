using System.Globalization;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>A list of CPUs as Linux writes one (<c>0-3</c>, <c>0,2-3</c>).</summary>
internal static class CpuSet
{
    // Far over any VM this stack runs on: a bound on a typo.
    private const int MostCpus = 1024;
    private const char Separator = ',';
    private const char Range = '-';

    /// <summary>The CPUs of <paramref name="text"/>; null when it is not a CPU list.</summary>
    public static IReadOnlySet<int>? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cpus = new SortedSet<int>();
        foreach (var part in text.Split(Separator, StringSplitOptions.TrimEntries))
        {
            if (RangeOf(part) is not var (first, last))
            {
                return null;
            }

            cpus.UnionWith(Enumerable.Range(first, last - first + 1));
        }

        return cpus;
    }

    private static (int First, int Last)? RangeOf(string part)
    {
        var bounds = part.Split(Range);
        if (bounds.Length > 2 || !TryCpu(bounds[0], out var first))
        {
            return null;
        }

        var last = first;
        if (bounds.Length == 2 && !TryCpu(bounds[1], out last))
        {
            return null;
        }

        return last >= first ? (first, last) : null;
    }

    private static bool TryCpu(string text, out int cpu) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out cpu)
        && cpu < MostCpus;
}
