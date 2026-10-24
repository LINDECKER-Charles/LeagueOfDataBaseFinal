namespace LoDb.Infrastructure.Robots.Identification;

/// <summary>
/// A robot as the catalog knows it: its key, its name and who runs it, what it does, the
/// tokens of its user agent and, when its operator documents them, the domains its reverse
/// DNS names end with.
/// </summary>
internal sealed record RobotProfile
{
    /// <summary>
    /// Stable and safe in a URL: <c>googlebot</c>, <c>undeclared:example.net</c>.
    /// </summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The company or project behind it; empty for a library.</summary>
    public required string Operator { get; init; }

    /// <summary>One of <see cref="RobotCategories"/>.</summary>
    public required string Category { get; init; }

    /// <summary>Lowercase fragments of its user agent, any of which names it.</summary>
    public IReadOnlyList<string> Tokens { get; init; } = [];

    /// <summary>Domains its genuine addresses reverse to; none when it cannot be checked.</summary>
    public IReadOnlyList<string> VerifyDomains { get; init; } = [];

    public bool IsVerifiable => VerifyDomains.Count > 0;

    /// <param name="agent">The user agent, lowercase.</param>
    public bool Matches(string agent) =>
        Tokens.Any(token => agent.Contains(token, StringComparison.Ordinal));
}
