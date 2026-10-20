namespace LoDb.Api.Modules.Admin.Prerender.Builds;

/// <summary>A file of a build, as the SSR serves it under <c>/build/</c>.</summary>
internal sealed record BuildFile
{
    /// <summary>Its name below <c>/build/</c>: <c>main-ABCD1234.js</c>, <c>media/…</c>.</summary>
    public required string Name { get; init; }

    public required ReadOnlyMemory<byte> Body { get; init; }

    public string? ContentType { get; init; }

    public string? ContentEncoding { get; init; }
}
