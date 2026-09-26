namespace LoDb.Api.Modules.Builds.Views;

/// <summary>A build's rune page on the patch it renders on.</summary>
internal sealed record RunePageView
{
    public required RunePathView Primary { get; init; }

    /// <summary>The pick of the primary path's first row.</summary>
    public required PerkView Keystone { get; init; }

    /// <summary>The picks of the primary path's three other rows, in row order.</summary>
    public required IReadOnlyList<PerkView> Minors { get; init; }

    public required RunePathView Secondary { get; init; }

    /// <summary>The picks of the secondary path, as the build stores them.</summary>
    public required IReadOnlyList<PerkView> SecondaryPerks { get; init; }
}
