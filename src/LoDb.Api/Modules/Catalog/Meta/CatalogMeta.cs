using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Catalog.Meta;

/// <summary>
/// Everything the front lists without hard-coding it: versions, languages, locales, modes.
/// </summary>
internal sealed record CatalogMeta
{
    /// <summary>
    /// The version the site and the apps open on; null until the first ingestion completes.
    /// </summary>
    public string? Latest { get; init; }

    /// <summary>Every version Data Dragon lists, newest first, the long tail included.</summary>
    public required IReadOnlyList<string> Versions { get; init; }

    /// <summary>The versions ingested ahead of any visit, newest first.</summary>
    public required IReadOnlyList<string> ReadyVersions { get; init; }

    /// <summary>Regular expression a version matches, anchors excluded.</summary>
    public required string VersionPattern { get; init; }

    /// <summary>Every Data Dragon language, such as en_US.</summary>
    public required IReadOnlyList<string> Languages { get; init; }

    /// <summary>Regular expression a language matches, anchors excluded.</summary>
    public required string LanguagePattern { get; init; }

    /// <summary>The language every version ships, which the others fall back to.</summary>
    public required string DefaultLanguage { get; init; }

    /// <summary>The interface locales and the Data Dragon language each one reads.</summary>
    public required IReadOnlyList<LocaleLanguage> Locales { get; init; }

    /// <summary>The locale of a visitor whose preference matches none.</summary>
    public required UiLocale FallbackLocale { get; init; }

    /// <summary>The modes a build targets, each with the map that gates its items.</summary>
    public required IReadOnlyList<GameModeMap> GameModes { get; init; }

    public required GameMode DefaultGameMode { get; init; }
}
