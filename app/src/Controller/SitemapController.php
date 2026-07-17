<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\AbstractManager;
use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use App\Service\Client\VersionManager;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * XML sitemap of every indexable page, generated from the live datasets.
 *
 * Only the default rendering is listed (latest Data Dragon version, no
 * version/lang query): per the canonical policy, query variants are alternate
 * renders of the same document, so exactly one crawlable URL exists per page.
 * Detail slugs are locale-invariant ids, hence a single reference language.
 */
final class SitemapController extends AbstractController
{
    /** Reference dataset for slug enumeration — ids are identical across languages. */
    private const SITEMAP_LANG = 'en_US';

    /** Datasets change on patch cadence (~biweekly); 1h keeps crawlers cheap and fresh enough. */
    private const CACHE_MAX_AGE = 3600;

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
    ];

    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly ChampionManager $champions,
        private readonly ItemManager $items,
        private readonly RuneManager $runes,
        private readonly SummonerManager $summoners,
    ) {}

    #[Route('/sitemap.xml', name: 'app_sitemap', methods: ['GET'])]
    public function sitemap(Request $request): Response
    {
        $host = $request->getSchemeAndHttpHost();
        $urls = array_map(
            static fn (string $path): string => $host . $path,
            [...self::STATIC_PATHS, ...$this->detailPaths()],
        );

        $response = new Response(
            $this->renderXml($urls),
            Response::HTTP_OK,
            ['Content-Type' => 'application/xml; charset=UTF-8'],
        );
        $response->setPublic();
        $response->setMaxAge(self::CACHE_MAX_AGE);

        return $response;
    }

    /** @return list<string> */
    private function detailPaths(): array
    {
        $version = $this->versionManager->getVersions()[0] ?? null;
        if ($version === null) {
            return [];
        }

        $sections = [
            '/champion/' => $this->champions,
            '/object/'   => $this->items,
            '/rune/'     => $this->runes,
            '/summoner/' => $this->summoners,
        ];

        $paths = [];
        foreach ($sections as $prefix => $manager) {
            foreach ($this->slugs($manager, $version) as $slug) {
                $paths[] = $prefix . rawurlencode($slug);
            }
        }

        return $paths;
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

    /** @param list<string> $urls */
    private function renderXml(array $urls): string
    {
        $writer = new \XMLWriter();
        $writer->openMemory();
        $writer->startDocument('1.0', 'UTF-8');
        $writer->startElement('urlset');
        $writer->writeAttribute('xmlns', 'http://www.sitemaps.org/schemas/sitemap/0.9');
        foreach ($urls as $url) {
            $writer->startElement('url');
            $writer->writeElement('loc', $url);
            $writer->endElement();
        }
        $writer->endElement();
        $writer->endDocument();

        return $writer->outputMemory();
    }
}
