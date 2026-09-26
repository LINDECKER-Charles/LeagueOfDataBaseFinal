using System.Globalization;
using System.Text;
using LoDb.Parity.Classification;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Runs;

/// <summary>The tables of <c>report.md</c>: coverage, deviation groups, languages.</summary>
internal static class ReportMarkdown
{
    private const int MaxCell = 240;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Of(ParityReport report)
    {
        var text = new StringBuilder();
        var sample = report.Run.Sample;
        text.AppendLine("# Parity run").AppendLine();
        text.AppendLine(Invariant, $"- Collected: {sample.CollectedAt}");
        text.AppendLine(Invariant, $"- Versions: {string.Join(", ", sample.Versions)}");
        text.AppendLine(Invariant, $"- Languages: {string.Join(", ", sample.Languages)}");
        text.AppendLine(Invariant, $"- Deviations: {report.Classified.Count} ({Classes(report)})");
        text.AppendLine();
        AppendTally(text, report.Run.Tally);
        AppendGroups(text, report);
        AppendCoverage(text, report.Run);
        return text.ToString();
    }

    private static string Classes(ParityReport report)
    {
        var counts = Enum.GetValues<DeviationClass>()
            .Select(c => $"{c} {report.OfClass(c).Count()}")
            .Append($"unclassified {report.Unclassified.Count()}");
        return string.Join(", ", counts);
    }

    private static void AppendTally(StringBuilder text, RunTally tally)
    {
        text.AppendLine("| Resource | Legacy entries | New entries |")
            .AppendLine("|---|---:|---:|");
        foreach (var (resource, (legacy, next)) in tally.Entries)
        {
            text.AppendLine(Invariant, $"| {resource} | {legacy} | {next} |");
        }

        text.AppendLine().AppendLine(Invariant, $"Champion details: {tally.DetailedChampions}")
            .AppendLine();
        text.AppendLine("| Manifest | Legacy keys | New keys | Shared |")
            .AppendLine("|---|---:|---:|---:|");
        foreach (var (type, (legacy, next, shared)) in tally.Keys)
        {
            text.AppendLine(Invariant, $"| {type} | {legacy} | {next} | {shared} |");
        }

        text.AppendLine();
    }

    private static void AppendGroups(StringBuilder text, ParityReport report)
    {
        text.AppendLine("| Group | Class | Count | Versions | Example |");
        text.AppendLine("|---|---|---:|---:|---|");
        foreach (var group in report.Groups)
        {
            var versions = group.Deviations.Select(static d => d.Site.Version).Distinct().Count();
            var example = Cell(Example(group.Deviations[0]));
            var name = Cell(group.Name);
            text.AppendLine(Invariant, $"| {name} | {group.Class?.ToString() ?? "—"} | "
                + $"{group.Deviations.Count} | {versions} | {example} |");
        }

        text.AppendLine();
    }

    private static void AppendCoverage(StringBuilder text, ParityRun run)
    {
        var languages = run.Sample.Languages;
        text.AppendLine(Invariant, $"| Version | {string.Join(" | ", languages)} |");
        text.AppendLine(Invariant, $"|---|{string.Concat(languages.Select(static _ => "---|"))}");
        foreach (var version in run.Sample.Versions)
        {
            var cells = languages
                .Select(language => run.Coverage
                    .Single(c => c.Version == version && c.Language == language))
                .Select(static c => c.IsFallback ? $"N/A {Side(c.Legacy)}, {Side(c.Next)}" : "✓");
            text.AppendLine(Invariant, $"| {version} | {string.Join(" | ", cells)} |");
        }
    }

    private static string Example(Deviation deviation)
    {
        var site = deviation.Site;
        var where = $"{site.Version}/{site.Language ?? "-"} {site.Resource} {site.Entry}";
        var values = $"`{Side(deviation.Legacy)}` → `{Side(deviation.Next)}`";
        return $"{where} {deviation.Field}: {values}";
    }

    private static string Side(string? value) => value ?? "∅";

    private static string Cell(string text)
    {
        var line = text.Replace('|', '¦').Replace('\n', ' ');
        return line.Length <= MaxCell ? line : string.Concat(line.AsSpan(0, MaxCell), "…");
    }
}
