using LoDb.Domain.Catalog.Champions;

namespace LoDb.Domain.Derived.Champions;

/// <summary>
/// Display name of a skin.
/// </summary>
/// <remarks>
/// Data Dragon names the base skin "default" in every language; it displays as the champion.
/// </remarks>
public static class SkinName
{
    /// <summary>Name Data Dragon gives the base skin.</summary>
    public const string DefaultSkinName = "default";

    public static string Display(Skin skin, string championName)
    {
        ArgumentNullException.ThrowIfNull(skin);
        return string.Equals(skin.Name, DefaultSkinName, StringComparison.Ordinal)
            ? championName
            : skin.Name;
    }
}
