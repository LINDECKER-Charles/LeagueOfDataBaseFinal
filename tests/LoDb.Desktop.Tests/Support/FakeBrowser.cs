using System.Collections.Concurrent;
using LoDb.Desktop.Shell;

namespace LoDb.Desktop.Tests.Support;

/// <summary>A system browser that records what it is asked to open.</summary>
internal sealed class FakeBrowser : ISystemBrowser
{
    public ConcurrentQueue<Uri> Opened { get; } = new();

    public bool IsAvailable { get; set; } = true;

    public bool Open(Uri address)
    {
        Opened.Enqueue(address);
        return IsAvailable;
    }
}
