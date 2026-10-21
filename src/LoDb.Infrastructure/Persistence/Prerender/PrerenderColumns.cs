using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// Stored spelling of the pass and unit states, which the check constraints of both tables
/// hold to and the admin shows.
/// </summary>
public static class PrerenderColumns
{
    public static readonly ValueConverter<PrerenderPassState, string> PassStateConverter = new(
        state => ToText(state),
        value => ParsePassState(value));

    public static readonly ValueConverter<PrerenderUnitState, string> UnitStateConverter = new(
        state => ToText(state),
        value => ParseUnitState(value));

    public static string ToText(PrerenderPassState state) => state switch
    {
        PrerenderPassState.Running => "running",
        PrerenderPassState.Paused => "paused",
        PrerenderPassState.Completed => "completed",
        PrerenderPassState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static PrerenderPassState ParsePassState(string value) => value switch
    {
        "running" => PrerenderPassState.Running,
        "paused" => PrerenderPassState.Paused,
        "completed" => PrerenderPassState.Completed,
        "cancelled" => PrerenderPassState.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToText(PrerenderUnitState state) => state switch
    {
        PrerenderUnitState.Pending => "pending",
        PrerenderUnitState.Running => "running",
        PrerenderUnitState.Completed => "completed",
        PrerenderUnitState.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static PrerenderUnitState ParseUnitState(string value) => value switch
    {
        "pending" => PrerenderUnitState.Pending,
        "running" => PrerenderUnitState.Running,
        "completed" => PrerenderUnitState.Completed,
        "failed" => PrerenderUnitState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
