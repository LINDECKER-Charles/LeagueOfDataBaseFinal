namespace LoDb.Desktop.Lifecycle;

/// <summary>Where the app's update stands, as the front's <c>UpdateState</c> reads it.</summary>
internal enum UpdateStage
{
    /// <summary>No newer version known.</summary>
    None,

    /// <summary>A newer version is being downloaded in the background.</summary>
    Downloading,

    /// <summary>A newer version is downloaded: applied on request or at exit.</summary>
    Ready,
}
