using LoDb.Ingestion.Catalog;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using static LoDb.Ingestion.Tests.Catalog.CacheHarness;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The gate of the cold loads, with its production defaults: a sweep of keys runs at most a
/// couple of loads, the public partition is refused at once and never takes the renderer's
/// last slot, the renderer waits a short while for its turn, and a reader joining a load
/// takes nothing.
/// </summary>
public sealed class ColdLoadGateTests
{
    private const int SimultaneousMisses = 50;
    private const int MaxLoads = 2;
    private const int Joiners = 20;
    private const int InternalKey = 1000;
    private const int LargeRate = 50_000;
    private const int RendererReads = 4;

    [Fact]
    public async Task FiftySimultaneousMissesRunAtMostTwoLoads()
    {
        using var harness = new CacheHarness(new CatalogOptions());
        using var admissions = Admissions(harness);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        var reads = Enumerable.Range(0, SimultaneousMisses)
            .Select(minor => harness.Cache.GetOrLoadAsync(
                Key(minor),
                Blocked(Key(minor), ColdCaller.Public, (release.Task, () => started++)),
                Token))
            .ToList();
        var running = reads.Where(static read => !read.IsCompleted).ToList();
        var refused = await Task.WhenAll(reads.Where(static read => read.IsCompleted));

        Assert.InRange(running.Count, 1, MaxLoads);
        Assert.All(refused, static read => Assert.True(read.IsRefused));
        release.SetResult();
        Assert.All(await Task.WhenAll(running), static read => Assert.True(read.IsHeld));
        Assert.InRange(started, 1, MaxLoads);
        Assert.Equal(
            SimultaneousMisses - running.Count,
            Count(admissions, CatalogMetrics.PublicPartition, CatalogMetrics.RefusedResult));
    }

    [Fact]
    public async Task InternalReadPassesWhilePublicPartitionIsFull()
    {
        using var harness = new CacheHarness(new CatalogOptions());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicLoad = harness.Cache.GetOrLoadAsync(
            Key(0),
            Blocked(Key(0), ColdCaller.Public, (release.Task, static () => { })),
            Token);
        var turnedAway = await harness.Cache.GetOrLoadAsync(
            Key(1),
            Build(Key(1), ColdCaller.Public),
            Token);

        var internalRead = await harness.Cache.GetOrLoadAsync(
            Key(InternalKey),
            Build(Key(InternalKey), ColdCaller.Internal),
            Token);

        Assert.True(turnedAway.IsRefused);
        Assert.True(internalRead.IsHeld);
        Assert.False(publicLoad.IsCompleted);
        release.SetResult();
        Assert.True((await publicLoad).IsHeld);
    }

    [Fact]
    public async Task InternalReadWaitsForAFreeSlot()
    {
        using var harness = new CacheHarness(new CatalogOptions());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocking = Enumerable.Range(0, MaxLoads)
            .Select(minor => harness.Cache.GetOrLoadAsync(
                Key(minor),
                Blocked(Key(minor), ColdCaller.Internal, (release.Task, static () => { })),
                Token))
            .ToList();

        var waiting = harness.Cache.GetOrLoadAsync(
            Key(InternalKey),
            Build(Key(InternalKey), ColdCaller.Internal),
            Token);
        Assert.False(waiting.IsCompleted);
        release.SetResult();

        Assert.True((await waiting).IsHeld);
        Assert.All(await Task.WhenAll(blocking), static read => Assert.True(read.IsHeld));
    }

    [Fact]
    public async Task InternalReadIsRefusedOnceItsWaitRunsOut()
    {
        var options = new CatalogOptions { InternalColdLoadWait = TimeSpan.FromMilliseconds(50) };
        using var harness = new CacheHarness(options);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocking = Enumerable.Range(0, MaxLoads)
            .Select(minor => harness.Cache.GetOrLoadAsync(
                Key(minor),
                Blocked(Key(minor), ColdCaller.Internal, (release.Task, static () => { })),
                Token))
            .ToList();

        var late = await harness.Cache.GetOrLoadAsync(
            Key(InternalKey),
            Build(Key(InternalKey), ColdCaller.Internal),
            Token);

        Assert.True(late.IsRefused);
        release.SetResult();
        Assert.All(await Task.WhenAll(blocking), static read => Assert.True(read.IsHeld));
        Assert.False(harness.Cache.TryGet(Key(InternalKey), out _));
    }

    [Fact]
    public async Task RendererKeepsItsShareOfTheBurstAtLargeRates()
    {
        // The burst times the renderer's rate exceeds what an int holds.
        var options = new CatalogOptions
        {
            ColdLoadConcurrency = 64,
            ColdLoadsPerSecond = LargeRate,
            PublicColdLoadsPerSecond = 1,
            ColdLoadBurst = LargeRate,
            InternalColdLoadWait = TimeSpan.FromMilliseconds(1),
        };
        using var harness = new CacheHarness(options);

        for (var minor = 0; minor < RendererReads; minor++)
        {
            var read = await harness.Cache.GetOrLoadAsync(
                Key(minor),
                Build(Key(minor), ColdCaller.Internal),
                Token);
            Assert.True(read.IsHeld, $"Internal load {minor} was refused.");
        }
    }

    [Fact]
    public async Task ReaderJoiningALoadTakesNothingFromTheGate()
    {
        using var harness = new CacheHarness(new CatalogOptions());
        using var admissions = Admissions(harness);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = Blocked(Key(0), ColdCaller.Public, (release.Task, static () => { }));

        var reads = Enumerable.Range(0, Joiners)
            .Select(_ => harness.Cache.GetOrLoadAsync(Key(0), load, Token))
            .ToList();
        release.SetResult();
        var joined = await Task.WhenAll(reads);
        var next = await harness.Cache.GetOrLoadAsync(
            Key(1),
            Build(Key(1), ColdCaller.Public),
            Token);

        Assert.All(joined, read => Assert.Same(joined[0].Catalog, read.Catalog));
        Assert.True(next.IsHeld);
        Assert.Equal(
            2,
            Count(admissions, CatalogMetrics.PublicPartition, CatalogMetrics.AdmittedResult));
    }

    private static MetricCollector<long> Admissions(CacheHarness harness) =>
        new(harness.Meters, CatalogMetrics.MeterName, "lodb.catalog.cold.admissions");

    private static long Count(MetricCollector<long> admissions, string partition, string result) =>
        admissions.GetMeasurementSnapshot()
            .Where(measurement =>
                Equals(measurement.Tags[CatalogMetrics.PartitionTag], partition)
                && Equals(measurement.Tags[CatalogMetrics.ResultTag], result))
            .Sum(static measurement => measurement.Value);
}
