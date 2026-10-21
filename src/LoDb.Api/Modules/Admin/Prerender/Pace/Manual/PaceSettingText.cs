using LoDb.Infrastructure.Persistence.Prerender.Pace;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>
/// The spelling of a <see cref="PaceChoice"/>, the same in the database, the admin's API and
/// the logs: <c>adaptive</c> or <c>manual</c>.
/// </summary>
internal static class PaceSettingText
{
    public static string Of(PaceChoice choice) => choice switch
    {
        PaceChoice.Adaptive => PrerenderPaceSetting.AdaptiveMode,
        PaceChoice.Manual => PrerenderPaceSetting.ManualMode,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, null),
    };

    /// <summary>The choice spelled <paramref name="text"/>; null for any other text.</summary>
    public static PaceChoice? Parse(string? text) => text switch
    {
        PrerenderPaceSetting.AdaptiveMode => PaceChoice.Adaptive,
        PrerenderPaceSetting.ManualMode => PaceChoice.Manual,
        _ => null,
    };
}
