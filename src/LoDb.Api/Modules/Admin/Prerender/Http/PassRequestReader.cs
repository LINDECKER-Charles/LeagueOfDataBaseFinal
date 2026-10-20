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
/// An epoch older than the served one is refused: nginx no longer reads it, and its retirement
/// may already have set the store to delete it.
/// </remarks>
internal static class PassRequestReader
{
    public const string EpochField = "epoch";
    public const string OldestField = "oldest";
    public const string NewestField = "newest";
    public const string PageTypeField = "pageType";
    public const string LocaleField = "locale";
    public const string PriorityField = "priority";

    /// <summary>An epoch below 1: epoch 0 is the store probes'.</summary>
    public const string OutOfRange = "out-of-range";

    /// <summary>An epoch below the served one.</summary>
    public const string Retired = "retired";

    /// <summary>A newest version below the oldest one.</summary>
    public const string BeforeOldest = "before-oldest";

    /// <summary>More priority versions than <see cref="MaxPriorityVersions"/>.</summary>
    public const string TooMany = "too-many";

    /// <summary>More than the patches of several years: a bound on a pasted log.</summary>
    public const int MaxPriorityVersions = 100;

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
        var oldest = VersionOf(errors, OldestField, request.Oldest);
        var newest = VersionOf(errors, NewestField, request.Newest);
        if (oldest is not null && newest is not null && newest < oldest)
        {
            errors.Add(NewestField, BeforeOldest);
        }

        return new PassScope
        {
            Oldest = oldest,
            Newest = newest,
            PageType = PageTypeOf(errors, request.PageType),
            Locale = LocaleOf(errors, request.Locale),
            Priority = PriorityOf(errors, request.Priority ?? []),
        };
    }

    private static PatchVersion? VersionOf(FieldErrors errors, string field, string? text)
    {
        errors.CheckShape(field, text, static value => PatchVersion.TryParse(value, out _));
        return PatchVersion.TryParse(text, out var version) ? version : null;
    }

    private static ResourceType? PageTypeOf(FieldErrors errors, string? text)
    {
        errors.CheckShape(PageTypeField, text, static value => PageTypes.TryParse(value, out _));
        return PageTypes.TryParse(text, out var type) ? type : null;
    }

    private static UiLocale? LocaleOf(FieldErrors errors, string? text)
    {
        errors.CheckShape(LocaleField, text, static value => UiLocales.TryParse(value, out _));
        return UiLocales.TryParse(text, out var locale) ? locale : null;
    }

    private static List<PatchVersion> PriorityOf(FieldErrors errors, IReadOnlyList<string> texts)
    {
        if (texts.Count > MaxPriorityVersions)
        {
            errors.Add(PriorityField, TooMany);
            return [];
        }

        var versions = new List<PatchVersion>(texts.Count);
        foreach (var text in texts)
        {
            if (!PatchVersion.TryParse(text, out var version))
            {
                errors.Add(PriorityField, FieldErrors.Malformed);
                return [];
            }

            versions.Add(version);
        }

        return versions;
    }
}
