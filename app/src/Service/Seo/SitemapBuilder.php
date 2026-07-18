<?php

declare(strict_types=1);

namespace App\Service\Seo;

use App\Service\API\AbstractManager;
use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use App\Service\Client\VersionManager;

/**
 * Builds the XML sitemap tree from the live datasets: a sitemap index pointing at
 * the primary sitemap (clean URLs, latest patch) plus one sitemap per historical
 * Data Dragon version (versioned `/{version}/…` URLs — the self-canonical patch
 * snapshots). The latest patch is served only by the primary sitemap; its
 * versioned form 301s away, so it is never listed here.
 *
 * Detail slugs are locale-invariant ids, so a single reference language enumerates
 * every page; the sitemap XML itself is language-agnostic by design (a standard).
 */
final class SitemapBuilder
{
    /** Reference dataset for slug enumeration — ids are identical across languages. */
    private const SITEMAP_LANG = 'en_US';

    /** Non-versioned pages — listed once, in the primary sitemap. */
    private const STATIC_PATHS = [
        '/',
        '/champions',
        '/objects',
        '/runes',
        '/summoners',
        '/legal/notice',
        '/legal/privacy',
        '/legal/terms',
        '/legal/cookies',
        '/donate',
        '/developers',
        '/trends',
        '/changelog',
    ];

    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly ChampionManager $champions,
        private readonly ItemManager $items,
        private readonly RuneManager $runes,
        private readonly SummonerManager $summoners,
    ) {}

    /** Sitemap index: the primary sitemap plus one entry per historical version. */
    public function indexXml(string $host): string
    {
        $versions = $this->versionManager->getVersions();
        $latest   = $versions[0] ?? null;

        $locs = [$host . '/sitemaps/latest.xml'];
        foreach ($versions as $version) {
            if ($version !== $latest) {
                $locs[] = $host . '/sitemaps/' . $version . '.xml';
            }
        }

        return $this->renderIndex($locs);
    }

    /** Primary sitemap: static pages + clean, canonical detail URLs for the latest patch. */
    public function latestXml(string $host): string
    {
        $paths  = self::STATIC_PATHS;
        $latest = $this->versionManager->getVersions()[0] ?? null;

        if ($latest !== null) {
            foreach ($this->sections() as [$prefix, $manager]) {
                foreach ($this->slugs($manager, $latest) as $slug) {
                    $paths[] = $prefix . rawurlencode($slug);
                }
            }
        }

        return $this->renderUrlset($host, $paths);
    }

    /** Per-version sitemap: versioned list + detail URLs for one historical patch. */
    public function versionXml(string $host, string $version): string
    {
        $paths = [];
        foreach ($this->sections() as [$prefix, $manager, $listPath]) {
            $paths[] = '/' . $version . $listPath;
            foreach ($this->slugs($manager, $version) as $slug) {
                $paths[] = '/' . $version . $prefix . rawurlencode($slug);
            }
        }

        return $this->renderUrlset($host, $paths);
    }

    public function isLatest(string $version): bool
    {
        return ($this->versionManager->getVersions()[0] ?? null) === $version;
    }

    /** @return list<array{0:string,1:AbstractManager,2:string}> detail-prefix, manager, list-path */
    private function sections(): array
    {
        return [
            ['/champion/', $this->champions, '/champions'],
            ['/object/',   $this->items,     '/objects'],
            ['/rune/',     $this->runes,     '/runes'],
            ['/summoner/', $this->summoners, '/summoners'],
        ];
    }

    /**
     * Best-effort slug list: an upstream failure skips the section instead of
     * turning the whole sitemap into a 500 (crawlers punish erroring sitemaps).
     *
     * @return list<string>
     */
    private function slugs(AbstractManager $manager, string $version): array
    {
        try {
            return array_map(strval(...), array_keys($manager->listIndex($version, self::SITEMAP_LANG)));
        } catch (\Throwable) {
            return [];
        }
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
