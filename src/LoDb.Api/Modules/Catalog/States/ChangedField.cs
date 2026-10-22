using LoDb.Domain.States.Fields;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// A field a state's opener changed, without its values: what the history and the patch page
/// list, one i18n label per <see cref="ChangeField"/>.
/// </summary>
internal sealed record ChangedField
{
    public required ChangeField Field { get; init; }

    /// <summary>The sub-entity of the field; null for a template without one.</summary>
    public required ChangeSubject? Subject { get; init; }
}
