namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>
/// The reverse DNS of an address: the name it reverses to, lowercase without its final dot,
/// and whether that name resolves back to it.
/// </summary>
internal sealed record ReverseLookup
{
    /// <summary>The address reverses to no name.</summary>
    public static ReverseLookup NoName { get; } = new();

    /// <summary>The lookup failed or timed out: the next batches may try again.</summary>
    public static ReverseLookup Failed { get; } = new() { HasFailed = true };

    public string? Host { get; init; }

    /// <summary>Whether <see cref="Host"/> resolves back to the address.</summary>
    public bool IsConfirmed { get; init; }

    public bool HasFailed { get; init; }

    /// <summary>Whether the name is confirmed and lies under one of the domains.</summary>
    public bool IsUnder(IReadOnlyList<string> domains) =>
        IsConfirmed && Host is { } host && domains.Any(domain =>
            host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));
}
