using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// Reads a <see cref="PassRequest"/> into the pass to launch, or the codes of its invalid
/// fields, all of them at once.
/// </summary>
/// <remarks>
/// <para>
/// An epoch older than the served one is refused: nginx no longer reads it, and its retirement
/// may already have set the store to delete it.
/// </para>
/// <para>
/// Each list keeps the first occurrence of an item; the locales and the types are put in the
/// site's order and a list that holds them all becomes null (<see cref="ScopeLists"/>). The
/// versions keep the order sent, and are never made null: only the client may say "every older
/// patch", since they change at each promotion.
/// </para>
/// </remarks>
internal static class PassRequestReader
{
    public const string EpochField = "epoch";
    public const string VersionsField = "versions";
    public const string LocalesField = "locales";
    public const string PageTypesField = "pageTypes";
    public const string PagesField = "pages";
    public const string PriorityField = "priority";

    /// <summary>The field of the refusals that concern the perimeter as a whole.</summary>
    public const string ScopeField = "scope";

    /// <summary>An epoch below 1: epoch 0 is the store probes'.</summary>
    public const string OutOfRange = "out-of-range";

    /// <summary>An epoch below the served one.</summary>
    public const string Retired = "retired";

    /// <summary>A list longer than its bound.</summary>
    public const string TooMany = "too-many";

    /// <summary>
    /// No version asked for is an older patch, or there is no older patch at all.
    /// </summary>
    public const string NonePinned = "none-pinned";

    /// <summary>Every unit of the perimeter is already complete in the epoch.</summary>
    public const string NothingToRender = "nothing-to-render";

    /// <summary>More than the patches of several years: a bound on a pasted log.</summary>
    public const int MaxPriorityVersions = 100;

    /// <summary>
    /// More than every older patch Data Dragon lists, about 400 in 2026. The locales and the
    /// types share the bound: repeated, they are still read once each.
    /// </summary>
    public const int MaxVersions = 1000;

    private static readonly ListRule VersionsRule = new(
        VersionsField,
        MaxVersions,
        IsRequired: true,
        static text => PatchVersion.TryParse(text, out _));

    private static readonly ListRule LocalesRule = new(
        LocalesField,
        MaxVersions,
        IsRequired: true,
        static text => UiLocales.TryParse(text, out _));

    private static readonly ListRule PageTypesRule = new(
        PageTypesField,
        MaxVersions,
        IsRequired: true,
        static text => PageTypes.TryParse(text, out _));

    private static readonly ListRule PriorityRule = new(
        PriorityField,
        MaxPriorityVersions,
        IsRequired: false,
        static text => PatchVersion.TryParse(text, out _));

    /// <summary>
    /// The pass <paramref name="request"/> asks for, launched by <paramref name="authorId"/>;
    /// or the validation problem naming each invalid field.
    /// </summary>
    public static (PassLaunch? Launch, AccountProblem? Problem) Read(
        PassRequest request,
        int servedEpoch,
        int authorId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new FieldErrors();
        var epoch = request.Epoch ?? servedEpoch;
        CheckEpoch(errors, epoch, servedEpoch);
        var scope = ScopeOf(request, errors);
        return errors.IsEmpty
            ? (new PassLaunch { Epoch = epoch, Scope = scope, AuthorId = authorId }, null)
            : (null, errors.ToProblem());
    }

    private static void CheckEpoch(FieldErrors errors, int epoch, int servedEpoch)
    {
        if (epoch < PrerenderOptions.FirstEpoch)
        {
            errors.Add(EpochField, OutOfRange);
        }
        else if (epoch < servedEpoch)
        {
            errors.Add(EpochField, Retired);
        }
    }

    private static PassScope ScopeOf(PassRequest request, FieldErrors errors)
    {
        var locales = TextsOf(errors, request.Locales, LocalesRule);
        var types = TextsOf(errors, request.PageTypes, PageTypesRule);
        return new PassScope
        {
            Versions = TextsOf(errors, request.Versions, VersionsRule)
                ?.Select(PatchVersion.Parse)
                .ToList(),
            Locales = locales is null ? null : ScopeLists.LocalesOf(locales.Select(LocaleOf)),
            PageTypes = types is null ? null : ScopeLists.TypesOf(types.Select(PageTypeOf)),
            Pages = PagesOf(errors, request.Pages),
            Priority = [.. (TextsOf(errors, request.Priority, PriorityRule) ?? [])
                .Select(PatchVersion.Parse)],
            SkipComplete = request.SkipComplete,
        };
    }

    // Every page unless the openers are asked for; anything else is refused on its field.
    private static PassPages PagesOf(FieldErrors errors, string? text)
    {
        if (PassScopeJson.PagesOf(text) is { } pages)
        {
            return pages;
        }

        errors.Add(PagesField, FieldErrors.Malformed);
        return PassPages.Every;
    }

    // The items of a list once each, in their order; null for a list not sent, and for one
    // refused, whose code goes to its field.
    private static List<string>? TextsOf(
        FieldErrors errors,
        IReadOnlyList<string?>? texts,
        ListRule rule)
    {
        if (texts is null)
        {
            return null;
        }

        var code = texts switch
        {
            { Count: 0 } when rule.IsRequired => FieldErrors.Required,
            _ when texts.Count > rule.Max => TooMany,
            _ when !texts.All(rule.IsWellFormed) => FieldErrors.Malformed,
            _ => null,
        };
        if (code is not null)
        {
            errors.Add(rule.Field, code);
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. texts.OfType<string>().Where(seen.Add)];
    }

    private static UiLocale LocaleOf(string text) =>
        UiLocales.TryParse(text, out var locale) ? locale : throw new FormatException(text);

    private static ResourceType PageTypeOf(string text) =>
        PageTypes.TryParse(text, out var type) ? type : throw new FormatException(text);

    // How a list of the request is read: its field, its bound, whether it may be empty, and
    // what each of its items must look like.
    private sealed record ListRule(
        string Field,
        int Max,
        bool IsRequired,
        Func<string?, bool> IsWellFormed);
}
