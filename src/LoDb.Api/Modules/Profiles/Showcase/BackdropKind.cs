namespace LoDb.Api.Modules.Profiles.Showcase;

/// <summary>What a profile's backdrop shows, so a page does not draw the champion twice.</summary>
internal enum BackdropKind
{
    /// <summary>The favorite skin.</summary>
    Skin,

    /// <summary>The base skin of the favorite champion.</summary>
    Champion,
}
