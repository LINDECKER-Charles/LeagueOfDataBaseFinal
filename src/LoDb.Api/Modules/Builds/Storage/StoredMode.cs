using LoDb.Domain.Catalog.Modes;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Storage;

/// <summary>The mode a stored build targets, from the code its column keeps.</summary>
internal static class StoredMode
{
    /// <summary>
    /// Its mode; Summoner's Rift for a code no mode has, the default every legacy row got.
    /// </summary>
    public static GameMode Of(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return GameModes.TryParse(build.GameMode, out var mode) ? mode : GameModes.Default;
    }
}
