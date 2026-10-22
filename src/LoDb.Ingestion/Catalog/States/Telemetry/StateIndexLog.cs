using LoDb.Domain.Catalog;
using Microsoft.Extensions.Logging;

namespace LoDb.Ingestion.Catalog.States.Telemetry;

/// <summary>
/// Summary lines of the index of states: one per update, one per batch of versions in a long
/// walk, one per load of a process's copy; never one per state.
/// </summary>
internal static partial class StateIndexLog
{
    /// <summary>The summary line of an update that took the lock.</summary>
    public static void Updated(ILogger logger, UpdateLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var summary = line.Summary;
        var byResource = summary?.ByResource ?? new Dictionary<ResourceType, int>();
        var champions = byResource.GetValueOrDefault(ResourceType.Champions);
        var items = byResource.GetValueOrDefault(ResourceType.Items);
        var runes = byResource.GetValueOrDefault(ResourceType.Runes);
        var summoners = byResource.GetValueOrDefault(ResourceType.Summoners);
        Updated(
            logger,
            line.Trigger,
            line.Outcome,
            summary?.GenerationId,
            summary?.CoverageStart?.Value,
            summary?.CoverageEnd?.Value,
            line.Versions,
            line.Ingested,
            line.Opened,
            line.Closed,
            summary?.States ?? 0,
            summary?.OpenStates ?? 0,
            champions,
            items,
            runes,
            summoners,
            line.DurationMs);
    }

    [LoggerMessage(
        EventName = "states.update.progress",
        Level = LogLevel.Information,
        Message = "The {Trigger} of the index of states indexed {Versions} versions of"
            + " generation {GenerationId}, through {Version}.")]
    public static partial void Progress(
        ILogger logger,
        string trigger,
        long generationId,
        string version,
        int versions);

    [LoggerMessage(
        EventName = "states.update.locked",
        Level = LogLevel.Warning,
        Message = "The {Trigger} of the index of states gave up: another writer holds"
            + " catalog:states. It and the reconciliation finish the work.")]
    public static partial void Locked(ILogger logger, string trigger);

    [LoggerMessage(
        EventName = "states.copy.loaded",
        Level = LogLevel.Information,
        Message = "Loaded the index of states: generation {GenerationId} through {CoverageEnd},"
            + " {States} states, in {DurationMs} ms.")]
    public static partial void CopyLoaded(
        ILogger logger,
        long? generationId,
        string? coverageEnd,
        int states,
        long durationMs);

    [LoggerMessage(
        EventName = "states.copy.failed",
        Level = LogLevel.Warning,
        Message = "The index of states could not be read; a copy is held: {HasCopy}.")]
    public static partial void CopyFailed(ILogger logger, bool hasCopy, Exception exception);

    [LoggerMessage(
        EventName = "states.update.completed",
        Level = LogLevel.Information,
        Message = "The {Trigger} of the index of states ended {Outcome}: generation"
            + " {GenerationId} covers {CoverageStart} to {CoverageEnd}; {Versions} versions"
            + " indexed, {Ingested} ingested, {Opened} states opened and {Closed} closed;"
            + " {States} states, {OpenStates} open ({Champions} champions, {Items} items,"
            + " {Runes} rune paths, {Summoners} summoner spells), in {DurationMs} ms.")]
    private static partial void Updated(
        ILogger logger,
        string trigger,
        string outcome,
        long? generationId,
        string? coverageStart,
        string? coverageEnd,
        int versions,
        int ingested,
        int opened,
        int closed,
        int states,
        int openStates,
        int champions,
        int items,
        int runes,
        int summoners,
        long durationMs);
}
