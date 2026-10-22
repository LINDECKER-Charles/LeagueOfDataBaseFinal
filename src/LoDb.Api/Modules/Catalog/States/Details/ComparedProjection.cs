using System.Collections.Frozen;
using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// The entity in the catalog compared to, as the block « Ce qui a changé » can set it against
/// the page: in the page's language, or as much of it as reads the same in every language.
/// </summary>
/// <remarks>
/// <para>
/// Data Dragon ships a language from some version on; before it, the catalog of that language
/// holds the en_US entries. Set against the page's, every text would read as rewritten whole.
/// A value written in words is then taken as the page has it, so it shows after alone, as one
/// whose translation did not change does. Numbers, codes, ids and the per-rank strings of
/// spells read the same in every language: they keep their value before.
/// </para>
/// <para>
/// A field written in words that the page no longer has is left out, its value before being
/// English: the block lists it without values.
/// </para>
/// </remarks>
internal static class ComparedProjection
{
    // Data Dragon writes these per-rank strings ("8/7/6/5/4") alike in every language.
    private static readonly FrozenSet<ChangeField> PerRankStrings = new[]
    {
        ChangeField.SpellCooldown,
        ChangeField.SpellCost,
        ChangeField.SpellRange,
    }.ToFrozenSet();

    /// <summary>
    /// The projection of <paramref name="entity"/> in <paramref name="compared"/>, to set
    /// against <paramref name="after"/>, its projection on the page; <paramref name="after"/>
    /// itself when the catalog compared lacks the entity: nothing then compares, every value
    /// shows after alone.
    /// </summary>
    public static IReadOnlyList<ProjectedField> Of(
        DetailEntity entity,
        CatalogSnapshot compared,
        IReadOnlyList<ProjectedField> after)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(compared);
        ArgumentNullException.ThrowIfNull(after);
        if (entity.Project(compared) is not { } before)
        {
            return after;
        }

        return entity.ContentLanguageIn(compared) == entity.ContentLanguageIn(entity.Catalog)
            ? before
            : InPageLanguage(before, after);
    }

    private static ProjectedField[] InPageLanguage(
        IReadOnlyList<ProjectedField> before,
        IReadOnlyList<ProjectedField> after)
    {
        var onPage = new Dictionary<FieldKey, ProjectedField>();
        foreach (var field in after)
        {
            onPage.TryAdd(field.Key, field);
        }

        return [.. before
            .Select(field => IsWrittenInWords(field.Key.Field)
                ? onPage.GetValueOrDefault(field.Key)
                : field)
            .OfType<ProjectedField>()];
    }

    private static bool IsWrittenInWords(ChangeField field)
    {
        var template = ChangeFields.Of(field);
        return template.Kind == FieldKind.Text
            || (template.Values == ValueKind.Text && !PerRankStrings.Contains(field));
    }
}
