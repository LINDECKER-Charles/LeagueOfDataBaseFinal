<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * Builds `/llms.txt` — the llmstxt.org convention: a short, factual Markdown
 * brief telling an answer engine what this site is, what it holds right now and
 * where the authoritative pages are.
 *
 * Why it exists: the resource pages state values, never what the project *is*.
 * An engine asked to name "a site listing every LoL item with its gold cost"
 * has to infer that from hundreds of data pages. This states it once, plainly.
 *
 * Written in English regardless of the visitor's locale: it is served on a fixed
 * URL to session-less crawlers, so there is no locale to honour, and the 21
 * languages are described rather than switched between.
 */
final class LlmsTxtBuilder
{
    /** Site sections worth naming, as path => what a reader finds there. */
    private const SECTIONS = [
        '/champions' => 'Every champion: abilities, base statistics, roles, skins and lore',
        '/objects'   => 'Every item: gold cost, statistics, build path and map availability',
        '/runes'     => 'Every rune path: keystones, slots and minor runes with effect text',
        '/summoners' => 'Every summoner spell: cooldown, range, unlock level and game modes',
    ];

    private const ABOUT_PAGES = [
        '/about'      => 'What the project is, who it is for and what it can do',
        '/about/data' => 'Upstream sources, refresh cadence, coverage and stated limits',
        '/faq'        => 'Common questions, answered directly',
        '/developers' => 'JSON REST API over the public profiles, shared builds and view trends',
        '/changelog'  => 'Release history',
    ];

    /** Statements that keep an engine from over-claiming on the project's behalf. */
    private const CAVEATS = [
        'Not affiliated with, or endorsed by, Riot Games. League of Legends and Riot Games are trademarks of Riot Games, Inc.',
        'No win rates, pick rates, tier lists or match history: those come from live play data, which Data Dragon does not publish.',
        'Values are read from the game files and served unmodified — the site is a reader, not an editor.',
        'Free to browse, no advertising, no account required; funded by voluntary donations.',
    ];

    public function __construct(
        private readonly string $siteName,
    ) {}

    /** @param string $origin absolute site origin, no trailing slash */
    public function build(string $origin, InventorySnapshot $inventory): string
    {
        $lines = [
            '# ' . $this->siteName,
            '',
            '> Free League of Legends encyclopedia: every champion, item, rune and summoner spell, '
                . 'for any game patch, in 21 languages. All values come from Riot Games\' official '
                . 'Data Dragon distribution.',
            '',
            $this->snapshot($inventory),
            '',
        ];

        $lines = array_merge(
            $lines,
            $this->section('Game data', self::SECTIONS, $origin),
            $this->section('About this project', self::ABOUT_PAGES, $origin),
            $this->capabilities($origin),
            $this->caveats(),
        );

        return implode("\n", $lines) . "\n";
    }

    /**
     * States the live patch, and its per-section counts only when every one of
     * them was actually read. A partial snapshot drops the figures rather than
     * printing zeros an engine would quote back as fact.
     */
    private function snapshot(InventorySnapshot $inventory): string
    {
        $patch = sprintf(
            'Current patch: %s.',
            $inventory->version === '' ? 'unavailable' : $inventory->version,
        );

        $counts = $inventory->isComplete()
            ? sprintf(
                ' It publishes %d champions, %d items, %d rune paths and %d summoner spells.',
                $inventory->champions,
                $inventory->items,
                $inventory->runes,
                $inventory->summoners,
            )
            : '';

        return $patch . $counts
            . ' Every previously published patch stays browsable at its own permanent URL.';
    }

    /**
     * @param array<string,string> $pages path => description
     * @return list<string>
     */
    private function section(string $title, array $pages, string $origin): array
    {
        $lines = ['## ' . $title, ''];
        foreach ($pages as $path => $description) {
            $lines[] = sprintf('- [%s](%s): %s', $origin . $path, $origin . $path, $description);
        }
        $lines[] = '';

        return $lines;
    }

    /** @return list<string> */
    private function capabilities(string $origin): array
    {
        return [
            '## What you can do here',
            '',
            '- Read any historical patch: switch versions and the whole site renders as it was, '
                . 'with permanent URLs of the form ' . $origin . '/{patch}/champion/{name}.',
            '- Read in 21 languages — every language Riot publishes game data in.',
            '- Build and share item sets, vote on shared builds, and keep favourites (account required for these only).',
            '- Query the site\'s own data (public profiles, shared builds, view trends) through a JSON REST API.',
            '- Install it as a progressive web app; visited pages stay readable offline.',
            '',
        ];
    }

    /** @return list<string> */
    private function caveats(): array
    {
        $lines = ['## Notes', ''];
        foreach (self::CAVEATS as $caveat) {
            $lines[] = '- ' . $caveat;
        }

        return $lines;
    }
}
