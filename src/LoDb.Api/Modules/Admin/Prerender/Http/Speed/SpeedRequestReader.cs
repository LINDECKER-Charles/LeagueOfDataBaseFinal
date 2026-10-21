using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>
/// Reads a <see cref="PrerenderSpeedRequest"/> into the setting to store, or the codes of its
/// invalid fields, all of them at once.
/// </summary>
internal static class SpeedRequestReader
{
    public const string ModeField = "mode";
    public const string RateField = "rendersPerMinute";

    /// <summary>A target under 1 or over the hard maximum.</summary>
    public const string OutOfRange = "out-of-range";

    /// <summary>
    /// The setting <paramref name="request"/> asks for, from <paramref name="current"/>: an
    /// adaptive request without a target keeps the current one.
    /// </summary>
    public static (PaceSetting? Setting, AccountProblem? Problem) Read(
        PrerenderSpeedRequest request,
        PaceSetting current,
        int manualMax)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(current);
        var errors = new FieldErrors();
        var mode = PaceSettingText.Parse(request.Mode);
        if (mode is null)
        {
            var code = request.Mode is null ? FieldErrors.Required : FieldErrors.Malformed;
            errors.Add(ModeField, code);
        }

        var rate = request.RendersPerMinute
            ?? (mode == PaceChoice.Manual ? null : current.RendersPerMinute);
        if (rate is null)
        {
            errors.Add(RateField, FieldErrors.Required);
        }
        else if (rate < 1 || (request.RendersPerMinute is not null && rate > manualMax))
        {
            errors.Add(RateField, OutOfRange);
        }

        return errors.IsEmpty
            ? (new PaceSetting(mode!.Value, rate!.Value), null)
            : (null, errors.ToProblem());
    }
}
