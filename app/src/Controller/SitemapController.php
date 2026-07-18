<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\Client\VersionManager;
use App\Service\Seo\SitemapBuilder;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Sitemap tree, generated from the live datasets: `/sitemap.xml` is the index,
 * `/sitemaps/latest.xml` the primary sitemap (clean URLs, latest patch) and
 * `/sitemaps/{version}.xml` a per-patch snapshot (versioned URLs). The latest
 * patch lives only in the primary sitemap — its versioned form 301s to it.
 */
final class SitemapController extends AbstractController
{
    /** Index + primary change on patch cadence (~biweekly); 1h keeps crawlers cheap and fresh. */
    private const CACHE_MAX_AGE = 3600;

    /** Historical patches are frozen data — cache their sitemaps aggressively. */
    private const IMMUTABLE_MAX_AGE = 31536000;

    public function __construct(
        private readonly SitemapBuilder $builder,
    ) {}

    #[Route('/sitemap.xml', name: 'app_sitemap', methods: ['GET'])]
    public function index(Request $request): Response
    {
        return $this->xml($this->builder->indexXml($request->getSchemeAndHttpHost()), self::CACHE_MAX_AGE);
    }

    #[Route('/sitemaps/latest.xml', name: 'app_sitemap_latest', methods: ['GET'])]
    public function latest(Request $request): Response
    {
        return $this->xml($this->builder->latestXml($request->getSchemeAndHttpHost()), self::CACHE_MAX_AGE);
    }

    #[Route('/sitemaps/{version}.xml', name: 'app_sitemap_version', requirements: ['version' => VersionManager::VERSION_PATTERN], methods: ['GET'])]
    public function version(Request $request, string $version): Response
    {
        // The latest patch is the primary sitemap's job — never duplicate it here.
        if ($this->builder->isLatest($version)) {
            return $this->redirectToRoute('app_sitemap_latest', [], Response::HTTP_MOVED_PERMANENTLY);
        }

        return $this->xml(
            $this->builder->versionXml($request->getSchemeAndHttpHost(), $version),
            self::IMMUTABLE_MAX_AGE,
            immutable: true,
        );
    }

    private function xml(string $body, int $maxAge, bool $immutable = false): Response
    {
        $response = new Response($body, Response::HTTP_OK, ['Content-Type' => 'application/xml; charset=UTF-8']);
        $response->setPublic();
        $response->setMaxAge($maxAge);
        if ($immutable) {
            $response->headers->addCacheControlDirective('immutable');
        }

        return $response;
    }
}
