using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Prerender.Demand;

/// <summary>
/// Stored spelling of the states of <c>prerender_demand</c>, which its check constraint holds
/// to and the queue's raw statements write.
/// </summary>
public static class PrerenderDemandColumns
{
    public static readonly ValueConverter<PrerenderDemandState, string> StateConverter = new(
        state => ToText(state),
        value => ParseState(value));

    public static string ToText(PrerenderDemandState state) => state switch
    {
        PrerenderDemandState.Pending => "pending",
        PrerenderDemandState.Rendering => "rendering",
        PrerenderDemandState.Done => "done",
        PrerenderDemandState.Failed => "failed",
        PrerenderDemandState.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static PrerenderDemandState ParseState(string value) => value switch
    {
        "pending" => PrerenderDemandState.Pending,
        "rendering" => PrerenderDemandState.Rendering,
        "done" => PrerenderDemandState.Done,
        "failed" => PrerenderDemandState.Failed,
        "skipped" => PrerenderDemandState.Skipped,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
