using LoDb.Domain.Languages;
using LoDb.Domain.States.Fields;

namespace LoDb.Domain.States.Diff;

/// <summary>
/// What a comparison renders: the fields the index marks changed (en_US keys), in the
/// language of the page, which also decides how texts split into tokens.
/// </summary>
public sealed record DiffScope
{
    public required DdragonLanguage Language { get; init; }

    public required IReadOnlySet<FieldKey> Keys { get; init; }
}
