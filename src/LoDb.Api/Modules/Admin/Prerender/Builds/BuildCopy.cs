namespace LoDb.Api.Modules.Admin.Prerender.Builds;

/// <summary>Where the build a page names stands, once the pass made sure of it.</summary>
internal enum BuildCopy
{
    /// <summary>Its files are all in the store: the page may be written.</summary>
    Stored,

    /// <summary>
    /// The SSR runs another build since it rendered the page, now copied: the page is rendered
    /// again, never written under a build whose files the store may lack.
    /// </summary>
    Superseded,
}
