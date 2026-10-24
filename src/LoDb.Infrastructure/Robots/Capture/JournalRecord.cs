using System.Net;

namespace LoDb.Infrastructure.Robots.Capture;

/// <summary>
/// One request as nginx reported it to the robots journal, once answered: when, from which
/// address, what was asked and how it was answered.
/// </summary>
/// <remarks>
/// The address and the user agent only serve the journal's pump: the hits keep the agent
/// and the network the address reverses to, never the address.
/// </remarks>
public sealed record JournalRecord
{
    /// <summary>When nginx answered.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The client's real address, as the edge forwarded it.</summary>
    public IPAddress? Address { get; init; }

    public required string Method { get; init; }

    /// <summary>The path and query requested, as sent.</summary>
    public required string Target { get; init; }

    public required short Status { get; init; }

    public required int DurationMs { get; init; }

    public required long Bytes { get; init; }

    public string? Agent { get; init; }

    /// <summary>nginx's cache status; null when no cache took part.</summary>
    public string? Cache { get; init; }
}
