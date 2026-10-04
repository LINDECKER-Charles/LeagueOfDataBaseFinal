<?php
declare(strict_types=1);

namespace App\Controller\Seo;

use App\Service\Seo\CanonicalHost;
use App\Service\Seo\LlmsTxtBuilder;
use App\Service\Seo\Inventory\SiteInventory;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Serves `/llms.txt` (llmstxt.org): a plain-Markdown brief describing the
 * project for answer engines. Content is derived from the live datasets, so the
 * patch and entry counts it states are the ones actually being served.
 */
final class LlmsTxtController extends AbstractController
{
    /** Tracks the patch cadence like the sitemap — an hour keeps crawlers cheap and current. */
    private const CACHE_MAX_AGE = 3600;

    public function __construct(
        private readonly LlmsTxtBuilder $builder,
        private readonly SiteInventory $inventory,
        private readonly CanonicalHost $canonicalHost,
    ) {}

    #[Route('/llms.txt', name: 'app_llms_txt', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $origin = $this->canonicalHost->origin(
            $request->getScheme(),
            $request->getHost(),
            $request->getPort()
        );

        $response = new Response(
            $this->builder->build($origin, $this->inventory->snapshot()),
            Response::HTTP_OK,
            ['Content-Type' => 'text/markdown; charset=UTF-8'],
        );
        $response->setPublic();
        $response->setMaxAge(self::CACHE_MAX_AGE);

        return $response;
    }
}
