using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The stored form of a <see cref="PassScope"/>, a JSON object whose fields are all optional:
/// <c>{"versions":["16.18.1","16.12.1"],"locales":["en","fr"],"pageTypes":["items"],
/// "pages":"openers","priority":["16.18.1"],"skipComplete":true}</c>. <c>{}</c> is every older
/// patch.
/// </summary>
/// <remarks>
/// Versions, types and locales are written as the URLs spell them, so that the admin and a
/// reader of the table see the same words. A scope stored before the lists (October 2026)
/// still reads: its range (<c>oldest</c>, <c>newest</c>), its one <c>pageType</c> and its one
/// <c>locale</c>. The former fields are written too, as far as they can say the scope: the
/// image before the lists ignores the new ones and would read <c>{}</c>, every older patch,
/// every type, every locale. A list of versions gives its bounds, one locale or one type its
/// own field; what they cannot say (several locales or types out of all) is read wider by that
/// image (docs/architecture/pieges.md). So is <c>pages</c>, which the image before it ignores:
/// it would read an openers pass as every page, its units' cursors in another list. Such a pass
/// is cancelled before going back to that image.
/// </remarks>
internal static class PassScopeJson
{
    /// <summary>The <c>pages</c> of an openers scope; absent, every page.</summary>
    public const string OpenersPages = "openers";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(PassScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var document = new Document
        {
            Versions = scope.Versions?.Select(static version => version.Value).ToList(),
            Oldest = (scope.Oldest ?? scope.Versions?.Min())?.Value,
            Newest = (scope.Newest ?? scope.Versions?.Max())?.Value,
            Locales = scope.Locales?.Select(UiLocales.Code).ToList(),
            PageTypes = scope.PageTypes?.Select(CanonicalPath.SegmentOf).ToList(),
            Pages = scope.Pages == PassPages.Openers ? OpenersPages : null,
            Priority = scope.Priority.Count == 0
                ? null
                : [.. scope.Priority.Select(static version => version.Value)],
            SkipComplete = scope.SkipComplete ? true : null,
            PageType = scope.PageTypes is [var type] ? CanonicalPath.SegmentOf(type) : null,
            Locale = scope.Locales is [var locale] ? UiLocales.Code(locale) : null,
        };
        return JsonSerializer.Serialize(document, Json);
    }

    /// <exception cref="FormatException">The text is not a scope this engine wrote.</exception>
    public static PassScope Read(string json)
    {
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Json);
        }
        catch (JsonException exception)
        {
            throw new FormatException("The scope of the pass is not a JSON object.", exception);
        }

        return document is null
            ? throw new FormatException("The scope of the pass is null.")
            : ScopeOf(document);
    }

    // A list wins over the bounds written beside it for the image before the lists.
    private static PassScope ScopeOf(Document document) => new()
    {
        Versions = document.Versions?.Select(PatchVersion.Parse).ToList(),
        Oldest = document.Oldest is null || document.Versions is not null
            ? null
            : PatchVersion.Parse(document.Oldest),
        Newest = document.Newest is null || document.Versions is not null
            ? null
            : PatchVersion.Parse(document.Newest),
        Locales = LocalesOf(document),
        PageTypes = TypesOf(document),
        Pages = PagesOf(document),
        Priority = [.. (document.Priority ?? []).Select(PatchVersion.Parse)],
        SkipComplete = document.SkipComplete ?? false,
    };

    // The list wins over the one locale of the former form, written beside it for that image.
    private static IReadOnlyList<UiLocale>? LocalesOf(Document document)
    {
        var codes = document.Locales ?? (document.Locale is { } one ? [one] : null);
        return codes is null ? null : ScopeLists.LocalesOf(codes.Select(LocaleOf));
    }

    private static IReadOnlyList<ResourceType>? TypesOf(Document document)
    {
        var names = document.PageTypes ?? (document.PageType is { } one ? [one] : null);
        return names is null ? null : ScopeLists.TypesOf(names.Select(PageTypeOf));
    }

    /// <summary>
    /// The pages <paramref name="text"/> names: every page when null, the openers for
    /// <see cref="OpenersPages"/>; null for any other text.
    /// </summary>
    public static PassPages? PagesOf(string? text) => text switch
    {
        null => PassPages.Every,
        OpenersPages => PassPages.Openers,
        _ => null,
    };

    private static PassPages PagesOf(Document document) =>
        PagesOf(document.Pages)
            ?? throw new FormatException($"'{document.Pages}' is not a set of pages.");

    private static ResourceType PageTypeOf(string text) =>
        PageTypes.TryParse(text, out var type)
            ? type
            : throw new FormatException($"'{text}' is not a type of page.");

    private static UiLocale LocaleOf(string text) =>
        UiLocales.TryParse(text, out var locale)
            ? locale
            : throw new FormatException($"'{text}' is not a locale of the site.");

    private sealed record Document
    {
        public IReadOnlyList<string>? Versions { get; init; }

        public string? Oldest { get; init; }

        public string? Newest { get; init; }

        public IReadOnlyList<string>? Locales { get; init; }

        public IReadOnlyList<string>? PageTypes { get; init; }

        public string? Pages { get; init; }

        public IReadOnlyList<string>? Priority { get; init; }

        public bool? SkipComplete { get; init; }

        // The former form: read, and written for the image before the lists.
        public string? PageType { get; init; }

        public string? Locale { get; init; }
    }
}
