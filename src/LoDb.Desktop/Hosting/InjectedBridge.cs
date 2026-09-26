namespace LoDb.Desktop.Hosting;

/// <summary>
/// The bridge transport that the served <c>index.html</c> hands to the front: the shell's
/// once the window runs, none otherwise (smoke check, or the page shown by the system
/// browser in the fallback, which has no bridge).
/// </summary>
internal sealed class InjectedBridge
{
    private volatile string? _transport;

    /// <summary>A JavaScript expression that evaluates to <c>{send, listen}</c>.</summary>
    public string? Transport => _transport;

    public void Use(string? transport) => _transport = transport;
}
