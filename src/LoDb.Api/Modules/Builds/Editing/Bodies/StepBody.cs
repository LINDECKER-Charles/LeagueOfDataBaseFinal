using LoDb.Domain.Builds.Structures;

namespace LoDb.Api.Modules.Builds.Editing.Bodies;

/// <summary>A step of a submitted purchase order.</summary>
internal sealed record StepBody
{
    /// <summary>1 to 40 characters once trimmed.</summary>
    public string? Label { get; init; }

    /// <summary>At most 300 characters once trimmed; a blank one is no note.</summary>
    public string? Note { get; init; }

    /// <summary>Item ids of the build's patch, in purchase order; duplicates allowed.</summary>
    public IReadOnlyList<string?>? Items { get; init; }

    public StepInput ToInput() => new() { Label = Label, Note = Note, Items = Items };
}
