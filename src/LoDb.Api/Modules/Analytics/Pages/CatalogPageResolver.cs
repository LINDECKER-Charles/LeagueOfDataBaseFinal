using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Egress.Errors;

namespace LoDb.Api.Modules.Analytics.Pages;

/// <summary>
/// Tells which page a requested address shows, and with which status, by the front's own
/// rules (ADR 0005): an address the front redirects is not counted, the page it lands on is.
/// </summary>
/// <remarks>
/// <para>
/// Counted: the home page, the lists and the details of the latest version. Not counted: a
/// catalogue page with a <c>?version=</c> Data Dragon lists, or a detail spelled otherwise
/// than its canonical path (both 301). A detail the latest version lacks is a 404; without
/// any version ingested, or without Data Dragon's lists, a catalogue page is a 503.
/// </para>
/// <para>
/// The language is the one the page shows: <c>?lang=</c> when Data Dragon lists it and it
/// is a variant of the locale's own language, the locale's language otherwise. The catalog
/// is only read as stored, in en_US, where every id and canonical path is the same.
/// </para>
/// </remarks>
internal sealed class CatalogPageResolver(ICatalogReader reader) : ITrackedPageResolver
{
    private const char RegionSeparator = '_';
    private const char PathSeparator = '/';

    public async Task<TrackedPage?> ResolveAsync(
        string target,
        CancellationToken cancellationToken)
    {
        if (PageAddressParser.Parse(target) is not { } address)
        {
            return null;
        }

        CatalogVersions versions;
        IReadOnlyList<DdragonLanguage> languages;
        try
        {
            versions = await reader.GetVersionsAsync(cancellationToken);
            languages = await reader.GetLanguagesAsync(cancellationToken);
        }
        catch (EgressException)
        {
            var own = UiLocales.DefaultLanguage(address.Locale).Code;
            return TrackedRoutes.Page(address, own, PageAnswer.Unavailable(IdOf(address)));
        }

        var answer = await AnswerAsync(address, versions, cancellationToken);
        return answer is null
            ? null
            : TrackedRoutes.Page(address, LanguageOf(address, languages), answer);
    }

    private async Task<PageAnswer?> AnswerAsync(
        PageAddress address,
        CatalogVersions versions,
        CancellationToken cancellationToken)
    {
        var queried = versions.Listed.FirstOrDefault(
            version => version.Value == address.Version);
        if (address.Resource is null)
        {
            // The home page reads ?version= in place: it never redirects.
            return PageAnswer.Found(null) with { Version = queried?.Value };
        }

        if (queried is not null)
        {
            return null;
        }

        if (versions.Latest is not { } latest)
        {
            return PageAnswer.Unavailable(IdOf(address));
        }

        return address.Entry is null
            ? PageAnswer.Found(null)
            : await DetailAsync(address, latest, cancellationToken);
    }

    private async Task<PageAnswer?> DetailAsync(
        PageAddress address,
        PatchVersion latest,
        CancellationToken cancellationToken)
    {
        if (address is not { Resource: { } resource, Entry: { } entry })
        {
            return null;
        }

        if (CanonicalPath.IdOf(resource, entry) is not { } id)
        {
            return PageAnswer.Missing(entry);
        }

        if (await CatalogAsync(latest, cancellationToken) is not { } catalog)
        {
            return PageAnswer.Unavailable(id);
        }

        if (CatalogEntities.Find(catalog, resource, id) is not { } found)
        {
            return PageAnswer.Missing(id);
        }

        var requested = CanonicalPath.SegmentOf(resource) + PathSeparator + entry;
        return found.CanonicalPath == requested ? PageAnswer.Found(found.Key) : null;
    }

    private async Task<CatalogSnapshot?> CatalogAsync(
        PatchVersion latest,
        CancellationToken cancellationToken)
    {
        var load = await reader.GetAsync(
            latest,
            DdragonLanguage.EnUs,
            ColdDemand.StoredOnly,
            cancellationToken);
        return load.IsReady ? load.Catalog : null;
    }

    // The id a detail's segment designates, or the segment itself; null off a detail.
    private static string? IdOf(PageAddress address) =>
        address is { Resource: { } resource, Entry: { } entry }
            ? CanonicalPath.IdOf(resource, entry) ?? entry
            : null;

    private static string LanguageOf(PageAddress address, IReadOnlyList<DdragonLanguage> listed)
    {
        var own = UiLocales.DefaultLanguage(address.Locale).Code;
        return address.Lang is { } lang
            && listed.Any(language => language.Code == lang)
            && PrimaryOf(lang) == PrimaryOf(own)
            ? lang
            : own;
    }

    // "en_GB" and "en_US" share "en": a variant never swaps the language of the locale.
    private static string PrimaryOf(string language)
    {
        var separator = language.IndexOf(RegionSeparator, StringComparison.Ordinal);
        return separator < 0 ? language : language[..separator];
    }
}
