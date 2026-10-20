namespace LoDb.Infrastructure.Persistence.Prerender;

/// <summary>
/// A row of <c>prerender_build</c>: a front build whose hashed files are already in the page
/// store, so that no pass copies them twice.
/// </summary>
public sealed class PrerenderBuild
{
    /// <summary>The length of a SHA-256 in hexadecimal.</summary>
    public const int HashLength = 64;

    /// <summary>The hash of the sorted file names the SSR reports for the build.</summary>
    public required string Hash { get; set; }

    public DateTimeOffset StoredAt { get; set; }

    /// <summary>
    /// Files this build added to the store. A file an earlier build already stored, under the
    /// same hashed name, counts with that build: summed over the builds, the counters are what
    /// <c>build/</c> holds, which the writer key cannot list.
    /// </summary>
    public int Files { get; set; }

    /// <summary>Bytes of <see cref="Files"/>, as stored.</summary>
    public long Bytes { get; set; }
}
