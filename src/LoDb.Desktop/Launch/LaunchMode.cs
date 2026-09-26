namespace LoDb.Desktop.Launch;

/// <summary>What one launch of the executable does.</summary>
internal enum LaunchMode
{
    /// <summary>The window over the loopback host.</summary>
    Window,

    /// <summary><c>--smoke</c>: start the host without a window, print its state, exit.</summary>
    Smoke,
}
