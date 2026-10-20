using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The stored form of a <see cref="PassScope"/>, a JSON object whose fields are all optional:
/// <c>{"oldest":"14.1.1","newest":"16.19.1","pageType":"items","locale":"fr",
/// "priority":["16.19.1"]}</c>. <c>{}</c> is every older patch.
/// </summary>
/// <remarks>
/// Versions, types and locales are written as the URLs spell them, so that the admin and a
/// reader of the table see the same words.
/// </remarks>
internal static class PassScopeJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(PassScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var document = new Document
        {
            Oldest = scope.Oldest?.Value,
            Newest = scope.Newest?.Value,
            PageType = scope.PageType is { } type ? CanonicalPath.SegmentOf(type) : null,
            Locale = scope.Locale is { } locale ? UiLocales.Code(locale) : null,
            Priority = scope.Priority.Count == 0
                ? null
                : [.. scope.Priority.Select(static version => version.Value)],
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

    private static PassScope ScopeOf(Document document) => new()
    {
        Oldest = document.Oldest is null ? null : PatchVersion.Parse(document.Oldest),
        Newest = document.Newest is null ? null : PatchVersion.Parse(document.Newest),
        PageType = document.PageType is null ? null : PageTypeOf(document.PageType),
        Locale = document.Locale is null ? null : LocaleOf(document.Locale),
        Priority = [.. (document.Priority ?? []).Select(PatchVersion.Parse)],
    };

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
        public string? Oldest { get; init; }

        public string? Newest { get; init; }

        public string? PageType { get; init; }

        public string? Locale { get; init; }

        public IReadOnlyList<string>? Priority { get; init; }
    }
}
