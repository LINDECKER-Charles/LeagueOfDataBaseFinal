namespace LoDb.Api.Modules.Legacy.Paths;

/// <summary>What an old URL showed, which decides what its redirect needs to know.</summary>
internal enum LegacyPathKind
{
    /// <summary>A page whose new path is known from the old one alone.</summary>
    Page,

    /// <summary>A catalog list, which may name a version.</summary>
    List,

    /// <summary>A catalog detail, whose old name the catalog turns into a canonical path.</summary>
    Detail,
}
