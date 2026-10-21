using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings;

/// <summary>Reads an SSR's load (<see cref="SsrLoadClient"/>; a fake in tests).</summary>
internal interface ISsrLoadReader
{
    /// <summary>The load at <paramref name="url"/>; null on any failure.</summary>
    Task<SsrLoad?> ReadAsync(Uri url, CancellationToken cancellationToken);
}
