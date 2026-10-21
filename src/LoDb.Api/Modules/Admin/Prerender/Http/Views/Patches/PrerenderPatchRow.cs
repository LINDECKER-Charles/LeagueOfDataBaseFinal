namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>One older patch in an epoch.</summary>
internal sealed record PrerenderPatchRow
{
    public required string Version { get; init; }

    /// <summary>One character per locale of Locales: c, w, p, f or - (absent).</summary>
    public required string Cells { get; init; }

    /// <summary>
    /// One hexadecimal character per locale of Locales: the mask of the types held (bit 0
    /// champions, 1 items, 2 runes, 3 summoners); f holds all four, 0 none.
    /// </summary>
    public required string Types { get; init; }

    /// <summary>Locales whose cell is c or w.</summary>
    public required int CompleteLocales { get; init; }

    /// <summary>Pages stored in the epoch for it, all locales.</summary>
    public required long Pages { get; init; }

    public required long Bytes { get; init; }

    /// <summary>Pages failed by the latest completed units, served by the SSR.</summary>
    public required long FailedPages { get; init; }

    /// <summary>
    /// Pages of one locale, as a completed unit over every type listed them, in any pass of any
    /// epoch (the most of them); null when none did.
    /// </summary>
    public int? PagesPerLocale { get; init; }

    /// <summary>When a unit of the epoch last ended for it; null when none did.</summary>
    public DateTimeOffset? LastEndedAt { get; init; }
}
