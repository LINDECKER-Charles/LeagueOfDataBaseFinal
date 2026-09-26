namespace LoDb.Parity.Deviations;

/// <summary>The facts a run attaches to deviations, for the rules to classify on.</summary>
public static class DeviationTags
{
    /// <summary>A champion the legacy stack holds the detail of (a visited detail page).</summary>
    public const string Detailed = "detailed";

    /// <summary>An item the browsable collection leaves out (debris), or its image.</summary>
    public const string Unlisted = "unlisted";

    /// <summary>A classic twin, or its image.</summary>
    public const string Classic = "classic";

    /// <summary>A champion portrait, the only champion image the legacy warmup stores.</summary>
    public const string Portrait = "portrait";

    /// <summary>A passive or ability icon, stored by the legacy stack on a detail visit.</summary>
    public const string Ability = "ability";

    /// <summary>A dataset Data Dragon lacks in the requested language (fallback).</summary>
    public const string Fallback = "fallback";
}
