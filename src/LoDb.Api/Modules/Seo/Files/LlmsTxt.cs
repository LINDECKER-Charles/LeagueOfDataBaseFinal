using System.Globalization;
using System.Text;
using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Seo.Files;

/// <summary>
/// The body of <c>/llms.txt</c>, the llmstxt.org convention: a short, factual Markdown brief
/// telling an answer engine what the site is, what it holds now and where its pages are.
/// </summary>
/// <remarks>
/// The data pages state values, never what the project is: this states it once, plainly.
/// Written in English whatever the visitor: it is served on a fixed URL to crawlers without
/// a locale, and it links the <c>en</c> pages.
/// </remarks>
internal static class LlmsTxt
{
    public const string ContentType = "text/markdown; charset=utf-8";

    private const string SiteName = "League Of Data Base";

    private static readonly int Languages = UiLocales.All.Count;

    // Paths below /{locale}/ with what a reader finds there.
    private static readonly (string Path, string About)[] GameData =
    [
        ("champions", "Every champion: abilities, base statistics, roles, skins and lore"),
        ("items", "Every item: gold cost, statistics, build path and map availability"),
        ("runes", "Every rune path: keystones, slots and minor runes with effect text"),
        ("summoners", "Every summoner spell: cooldown, range, unlock level and game modes"),
    ];

    private static readonly (string Path, string About)[] AboutPages =
    [
        ("about", "What the project is, who it is for and what it can do"),
        ("about/data", "Upstream sources, refresh cadence, coverage and stated limits"),
        ("faq", "Common questions, answered directly"),
        ("developers", "JSON REST API over the public profiles, shared builds and view trends"),
        ("changelog", "Release history"),
    ];

    // Statements that keep an engine from over-claiming on the project's behalf.
    private static readonly string[] Caveats =
    [
        "Not affiliated with, or endorsed by, Riot Games. League of Legends and Riot Games are "
            + "trademarks of Riot Games, Inc.",
        "No win rates, pick rates, tier lists or match history: those come from live play "
            + "data, which Data Dragon does not publish.",
        "Values are read from the game files and served unmodified: the site is a reader, "
            + "not an editor.",
        "Free to browse, no advertising, no account required; funded by voluntary donations.",
    ];

    /// <param name="origin">Canonical origin, no trailing slash.</param>
    /// <param name="inventory">What the site publishes now.</param>
    public static string Build(string origin, SiteInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var pages = SitePages.Prefix(origin, UiLocales.Fallback, null);
        var body = new StringBuilder()
            .Append("# ").Append(SiteName).Append("\n\n")
            .Append("> Free League of Legends encyclopedia: every champion, item, rune and ")
            .Append("summoner spell, for any game patch, in ")
            .Append(CultureInfo.InvariantCulture, $"{Languages} languages. All values come from ")
            .Append("Riot Games' official Data Dragon distribution.\n\n")
            .Append(Snapshot(inventory)).Append("\n\n");
        AppendList(body, "Game data", Links(GameData, pages));
        AppendList(body, "About this project", Links(AboutPages, pages));
        AppendCapabilities(body, pages);
        AppendList(body, "Notes", Caveats);

        // Each list closes with a blank line; the file ends with a single line break.
        return body.ToString().TrimEnd('\n') + "\n";
    }

    // The counts appear only when the catalog was read: a partial brief would print figures
    // an engine quotes back as fact.
    private static string Snapshot(SiteInventory inventory)
    {
        var patch = $"Current patch: {inventory.Version?.Value ?? "unavailable"}.";
        var counts = inventory.Catalog is { } catalog
            ? string.Create(
                CultureInfo.InvariantCulture,
                $" It publishes {catalog.Champions.Entries.Count} champions, "
                + $"{catalog.ListedItems.Count} items, {catalog.Runes.Entries.Count} rune "
                + $"paths and {catalog.Summoners.Entries.Count} summoner spells.")
            : string.Empty;
        return patch + counts
            + " Every previously published patch stays browsable at its own permanent URL.";
    }

    private static IEnumerable<string> Links(
        IEnumerable<(string Path, string About)> pages,
        string prefix) =>
        pages.Select(page => $"[{prefix}{page.Path}]({prefix}{page.Path}): {page.About}");

    private static void AppendCapabilities(StringBuilder body, string prefix) =>
        AppendList(body, "What you can do here",
        [
            "Read any historical patch: switch versions and the whole site renders as it was, "
                + $"with permanent URLs of the form {prefix}{{patch}}/champions/{{id}}.",
            $"Read in {Languages} languages: every language Riot publishes game data in.",
            "Build and share item sets, vote on shared builds, and keep favourites "
                + "(account required for these only).",
            "Query the site's own data (public profiles, shared builds, view trends) "
                + "through a JSON REST API.",
            "Install it as a progressive web app; visited pages stay readable offline.",
        ]);

    private static void AppendList(StringBuilder body, string title, IEnumerable<string> lines)
    {
        body.Append("## ").Append(title).Append("\n\n");
        foreach (var line in lines)
        {
            body.Append("- ").Append(line).Append('\n');
        }

        body.Append('\n');
    }
}
