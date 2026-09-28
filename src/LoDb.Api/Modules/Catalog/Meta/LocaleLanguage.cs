using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Catalog.Meta;

/// <summary>An interface locale and the Data Dragon language its pages read by default.</summary>
internal sealed record LocaleLanguage
{
    public required UiLocale Locale { get; init; }

    public required string Language { get; init; }
}
