using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// The host's <c>/proc/stat</c> (guest time never counted twice, its CPUs counted, a set of CPUs
/// for the bench),
/// the API's <c>cpu.stat</c>, and the CPU lists the bench sets.
/// </summary>
public sealed class PaceReadingsTests
{
    private const string ProcStat = """
        cpu  1000 100 500 8000 200 50 50 100 70 30
        cpu0 250 25 125 2000 50 10 10 30 20 10
        cpu1 250 25 125 2000 50 10 10 20 20 10
        cpu2 250 25 125 2000 50 15 15 25 15 5
        cpu3 250 25 125 2000 50 15 15 25 15 5
        intr 123456 0 0
        ctxt 987654
        """;

    [Fact]
    public void AggregateLineCountsEveryCpuWithoutTheGuestTimeAndTheirNumber()
    {
        var sample = ProcStatReader.Parse(ProcStat, cpus: null);

        Assert.Equal(new HostCpuSample(8000, 100, 10000, Cpus: 4), sample);
    }

    [Fact]
    public void CpuSetSumsItsOwnLines()
    {
        var sample = ProcStatReader.Parse(ProcStat, CpuSet.Parse("0,2-3"));

        Assert.Equal(new HostCpuSample(6000, 80, 7510, Cpus: 3), sample);
    }

    [Theory]
    [InlineData("cpu  1 2 3 x 5 6 7 8 9 10\n")]
    [InlineData("cpu  1 2 3 4 5 6 7\n")]
    [InlineData("intr 1 2 3\n")]
    [InlineData("")]
    public void MalformedProcStatReadsAsNothing(string text)
    {
        Assert.Null(ProcStatReader.Parse(text, cpus: null));
    }

    [Fact]
    public void CpuSetWithACpuTheHostLacksReadsAsNothing()
    {
        Assert.Null(ProcStatReader.Parse(ProcStat, CpuSet.Parse("0-4")));
    }

    [Theory]
    [InlineData("0-3", new[] { 0, 1, 2, 3 })]
    [InlineData("0,2-3", new[] { 0, 2, 3 })]
    [InlineData(" 5 ", new[] { 5 })]
    public void CpuListParses(string text, int[] cpus)
    {
        Assert.Equal(cpus, CpuSet.Parse(text)!.ToArray());
    }

    [Theory]
    [InlineData("3-1")]
    [InlineData("0-")]
    [InlineData("a")]
    [InlineData("0,,1")]
    [InlineData("-1")]
    [InlineData("0-2048")]
    [InlineData("")]
    public void NotACpuListParsesAsNothing(string text)
    {
        Assert.Null(CpuSet.Parse(text));
    }

    [Fact]
    public void CgroupStatGivesTheUsageAndTheThrottledTime()
    {
        const string stat = """
            usage_usec 9000000
            user_usec 6000000
            system_usec 3000000
            nr_periods 100
            nr_throttled 4
            throttled_usec 250000
            """;

        Assert.Equal(new CgroupCpuSample(9000000, 250000), CgroupCpuReader.Parse(stat));
        Assert.Equal(new CgroupCpuSample(12, 0), CgroupCpuReader.Parse("usage_usec 12\n"));
        Assert.Null(CgroupCpuReader.Parse("throttled_usec 1\n"));
    }
}
