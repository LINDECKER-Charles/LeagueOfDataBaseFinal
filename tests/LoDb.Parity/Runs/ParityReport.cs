using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Parity.Classification;

namespace LoDb.Parity.Runs;

/// <summary>
/// The classified deviations of a run, written next to it as <c>report.md</c> (tables the
/// parity report quotes), <c>deviations.json</c> (every group, with examples) and
/// <c>deviations.jsonl</c> (every deviation with its rule, one per line).
/// </summary>
public sealed class ParityReport
{
    private const int ExamplesPerGroup = 25;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions LineOptions = new(JsonOptions)
    {
        WriteIndented = false,
    };

    private ParityReport(ParityRun run, IReadOnlyList<ClassifiedDeviation> classified)
    {
        Run = run;
        Classified = classified;
        Groups = DeviationGroup.Of(classified);
    }

    public ParityRun Run { get; }

    public IReadOnlyList<ClassifiedDeviation> Classified { get; }

    public IReadOnlyList<DeviationGroup> Groups { get; }

    public IEnumerable<ClassifiedDeviation> Unclassified =>
        Classified.Where(static c => c.Rule is null);

    public static ParityReport Of(ParityRun run, IReadOnlyList<DeviationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new ParityReport(run, Classifier.Classify(run.Deviations(), rules));
    }

    public IEnumerable<ClassifiedDeviation> OfClass(DeviationClass deviationClass) =>
        Classified.Where(c => c.Rule?.Class == deviationClass);

    public void WriteTo(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "report.md"), ReportMarkdown.Of(this));
        var groups = Groups.Select(static group => new
        {
            group.Name,
            group.Class,
            Count = group.Deviations.Count,
            Examples = group.Deviations.Take(ExamplesPerGroup),
        });
        File.WriteAllText(
            Path.Combine(directory, "deviations.json"),
            JsonSerializer.Serialize(groups, JsonOptions));
        File.WriteAllLines(
            Path.Combine(directory, "deviations.jsonl"),
            Classified.Select(static c => JsonSerializer.Serialize(
                new { Rule = c.Rule?.Id, c.Deviation },
                LineOptions)));
    }
}
