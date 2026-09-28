namespace LoDb.Domain.Paths;

/// <summary>
/// The three unversioned champion art families of the Data Dragon CDN.
/// </summary>
public enum ChampionArtKind
{
    /// <summary>Wide 1215×717 splash art.</summary>
    Splash,

    /// <summary>Portrait 308×560 loading-screen crop.</summary>
    Loading,

    /// <summary>Centered 1280×720 crop of the client's champion select.</summary>
    Centered,
}
