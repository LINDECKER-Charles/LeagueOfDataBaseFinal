namespace LoDb.Api.Modules.Builds.Views;

/// <summary>A step of a build's purchase order, priced on the patch it renders on.</summary>
internal sealed record StepView
{
    public required string Label { get; init; }

    public string? Note { get; init; }

    /// <summary>In purchase order, duplicates and ghosts kept.</summary>
    public required IReadOnlyList<ItemView> Items { get; init; }

    /// <summary>The cost of the step's items, ghosts left out.</summary>
    public required int Gold { get; init; }
}
