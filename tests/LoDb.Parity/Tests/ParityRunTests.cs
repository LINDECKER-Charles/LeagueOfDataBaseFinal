using LoDb.Parity.Classification;
using LoDb.Parity.Runs;

namespace LoDb.Parity.Tests;

/// <summary>
/// The parity check itself, on a run collected by tools/next/parity: skipped unless
/// <c>LODB_PARITY_RUN</c> names its directory, since it needs both stacks warmed.
/// </summary>
public sealed class ParityRunTests
{
    private const string RunVariable = "LODB_PARITY_RUN";

    private static readonly Lazy<ParityReport?> Report = new(static () =>
    {
        var directory = Environment.GetEnvironmentVariable(RunVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var report = ParityReport.Of(ParityRun.Open(directory), ParityRules.All);
        report.WriteTo(directory);
        return report;
    });

    [Fact]
    public void EveryDeviationIsClassified()
    {
        var report = RunReport();

        Assert.Empty(report.Unclassified.Select(static c => DeviationGroup.ShapeOf(c.Deviation))
            .Distinct());
    }

    [Fact]
    public void NoDefectOfTheNewStackIsLeft()
    {
        var report = RunReport();

        Assert.Empty(report.OfClass(DeviationClass.NextDefect).Select(static c => c.Rule!.Id)
            .Distinct());
    }

    private static ParityReport RunReport()
    {
        Assert.SkipWhen(Report.Value is null, $"{RunVariable} names no parity run.");
        return Report.Value!;
    }
}
