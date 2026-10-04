namespace LoDb.Domain.Languages;

/// <summary>
/// Interface locale of the site, carried as the first URL segment (<c>/{locale}/…</c>).
/// </summary>
/// <remarks>
/// One catalog per language, except Chinese, which keeps its two scripts. The code of each
/// value (<see cref="UiLocales.Code"/>) is its kebab-case name, which is what the API
/// serializer writes with the kebab-case naming policy: the OpenAPI enum and the URL segment
/// agree.
/// </remarks>
public enum UiLocale
{
    Ar,
    Cs,
    De,
    El,
    En,
    Es,
    Fr,
    Hu,
    Id,
    It,
    Ja,
    Ko,
    Pl,
    Pt,
    Ro,
    Ru,
    Th,
    Tr,
    Vi,
    ZhHans,
    ZhHant,
}
