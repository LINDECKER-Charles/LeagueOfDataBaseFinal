<?php

declare(strict_types=1);

namespace App\Service\Seo;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use App\Service\Client\VersionManager;
use App\Service\Seo\Inventory\DatasetIndexReader;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * Builds the XML sitemap tree from the live datasets: a sitemap index pointing at
 * the primary sitemap (clean URLs, latest patch) plus one sitemap per historical
 * Data Dragon version (versioned `/{version}/…` URLs — the self-canonical patch
 * snapshots). The latest patch is served only by the primary sitemap; its
 * versioned form 301s away, so it is never listed here.
 *
 * Pages are named by route, never by literal path: a renamed route would
 * otherwise silently publish a sitemap full of 404s.
 *
 * Detail slugs are locale-invariant ids, so a single reference language enumerates
 * every page; the sitemap XML itself is language-agnostic by design (a standard).
 */
final class SitemapBuilder
{
    /** Non-versioned pages — listed once, in the primary sitemap, in this order. */
    private const STATIC_ROUTES = [
        'app_home',
        'app_champions',
        'app_items',
        'app_runes',
        'app_summoners',
        'app_about',
        'app_about_data',
        'app_faq',
        'app_legal_notice',
        'app_legal_privacy',
        'app_legal_terms',
        'app_legal_cookies',
        'app_donate',
        'app_developers',
        'app_trends',
        'app_changelog',
    ];

    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly UrlGeneratorInterface $router,
        private readonly DatasetIndexReader $datasets,
        private readonly ChampionManager $champions,
        private readonly ItemManager $items,
        private readonly RuneManager $runes,
        private readonly SummonerManager $summoners,
    ) {}

    /** Sitemap index: the primary sitemap plus one entry per historical version. */
    public function indexXml(string $host): string
    {
        $latest = $this->versionManager->latestVersion();

        $locs = [$host . '/sitemaps/latest.xml'];
        foreach ($this->versionManager->getVersions() as $version) {
            if ($version !== $latest) {
                $locs[] = $host . '/sitemaps/' . $version . '.xml';
            }
        }

        return $this->renderIndex($locs);
    }

    /** Primary sitemap: static pages + clean, canonical detail URLs for the latest patch. */
    public function latestXml(string $host): string
    {
        $paths  = $this->staticPaths();
        $latest = $this->versionManager->latestVersion();

        if ($latest !== '') {
            foreach ($this->sections() as $section) {
                foreach ($this->slugs($section, $latest) as $slug) {
                    $paths[] = $section->detailPrefix . rawurlencode($slug);
                }
            }
        }

        return $this->renderUrlset($host, $paths);
    }

    /** Per-version sitemap: versioned list + detail URLs for one historical patch. */
    public function versionXml(string $host, string $version): string
    {
        $paths = [];
        foreach ($this->sections() as $section) {
            $paths[] = '/' . $version . $section->listPath;
            foreach ($this->slugs($section, $version) as $slug) {
                $paths[] = '/' . $version . $section->detailPrefix . rawurlencode($slug);
            }
        }

        return $this->renderUrlset($host, $paths);
    }

    public function isLatest(string $version): bool
    {
        return $version !== '' && $this->versionManager->latestVersion() === $version;
    }

    /** @return list<string> */
    private function staticPaths(): array
    {
        return array_map($this->path(...), self::STATIC_ROUTES);
    }

    /** @return list<SitemapSection> */
    private function sections(): array
    {
        return [
            new SitemapSection('/champion/', $this->champions, $this->path('app_champions')),
            new SitemapSection('/object/', $this->items, $this->path('app_items')),
            new SitemapSection('/rune/', $this->runes, $this->path('app_runes')),
            new SitemapSection('/summoner/', $this->summoners, $this->path('app_summoners')),
        ];
    }

    private function path(string $route): string
    {
        return $this->router->generate($route);
    }

    /**
     * Best-effort slug list: an upstream failure skips the section instead of
     * turning the whole sitemap into a 500 (crawlers punish erroring sitemaps).
     *
     * @return list<string>
     */
    private function slugs(SitemapSection $section, string $version): array
    {
        $index = $this->datasets->read($section->manager, $version) ?? [];

        return array_map(strval(...), array_keys($index));
    }

    /** @param list<string> $paths */
    private function renderUrlset(string $host, array $paths): string
    {
        $writer = $this->openDocument('urlset');
        foreach ($paths as $path) {
            $writer->startElement('url');
            $writer->writeElement('loc', $host . $path);
            $writer->endElement();
        }

        return $this->closeDocument($writer);
    }

    /** @param list<string> $locs */
    private function renderIndex(array $locs): string
    {
        $writer = $this->openDocument('sitemapindex');
        foreach ($locs as $loc) {
            $writer->startElement('sitemap');
            $writer->writeElement('loc', $loc);
            $writer->endElement();
        }

        return $this->closeDocument($writer);
    }

    private function openDocument(string $root): \XMLWriter
    {
        $writer = new \XMLWriter();
        $writer->openMemory();
        $writer->startDocument('1.0', 'UTF-8');
        $writer->startElement($root);
        $writer->writeAttribute('xmlns', 'http://www.sitemaps.org/schemas/sitemap/0.9');

        return $writer;
    }

    private function closeDocument(\XMLWriter $writer): string
    {
        $writer->endElement();
        $writer->endDocument();

        return $writer->outputMemory();
    }
}
