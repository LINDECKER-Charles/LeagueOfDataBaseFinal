<?php
declare(strict_types=1);

namespace App\Controller\Api;

use App\Service\Client\PageContextResolver;
use App\Service\Picker\GameMode;
use App\Service\Picker\PickerCatalog;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpKernel\EventListener\AbstractSessionListener;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Read-only JSON catalogue backing the favorite/build pickers. Context follows
 * the site rule (valid ?version=&lang= query wins, session otherwise); the
 * payload version/lang echo what was actually served so islands can cache.
 */
final class PickerController extends AbstractController
{
    private const PUBLIC_TTL_SECONDS = 3600;

    public function __construct(
        private readonly PickerCatalog $catalog,
        private readonly PageContextResolver $pageContext,
    ) {}

    #[Route('/api/picker/champions', name: 'api_picker_champions', methods: ['GET'])]
    public function champions(Request $request): JsonResponse
    {
        return $this->catalogResponse($request, 'options', $this->catalog->championOptions(...));
    }

    #[Route('/api/picker/items', name: 'api_picker_items', methods: ['GET'])]
    public function items(Request $request): JsonResponse
    {
        // Unknown ?mode= degrades to the default deterministically (URL-stable,
        // no session input), so the cache-policy URL check stays sound.
        $mode = GameMode::fromForm($request->query->get('mode')) ?? GameMode::DEFAULT;

        return $this->catalogResponse(
            $request,
            'options',
            fn (string $version, string $lang): array => $this->catalog->itemOptions($version, $lang, $mode),
            ['mode' => $mode->value],
        );
    }

    #[Route('/api/picker/runes', name: 'api_picker_runes', methods: ['GET'])]
    public function runes(Request $request): JsonResponse
    {
        return $this->catalogResponse($request, 'trees', $this->catalog->runeTrees(...));
    }

    #[Route('/api/picker/summoners', name: 'api_picker_summoners', methods: ['GET'])]
    public function summoners(Request $request): JsonResponse
    {
        return $this->catalogResponse($request, 'options', $this->catalog->summonerOptions(...));
    }

    /**
     * @param callable(string, string): list<array<string, mixed>> $load
     * @param array<string, string>                                $extra echoed payload fields (e.g. items' mode)
     */
    private function catalogResponse(Request $request, string $collectionKey, callable $load, array $extra = []): JsonResponse
    {
        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();

        try {
            $collection = $load($version, $lang);
        } catch (\Throwable) {
            return new JsonResponse(
                ['error' => sprintf('Picker data unavailable for version %s / language %s.', $version, $lang)],
                Response::HTTP_SERVICE_UNAVAILABLE,
            );
        }

        $served = ['version' => $version, 'lang' => $lang] + $extra;
        $response = new JsonResponse($served + [$collectionKey => $collection]);
        $this->applyCachePolicy($request, $response, $served);

        return $response;
    }

    /**
     * Explicit VALID (version, lang) in the query → the URL fully determines the
     * payload: shareable and safe for shared caches. Anything else fell back to
     * the session and must stay private. Invalid explicit params resolve to the
     * session too, hence the equality check instead of a bare has() — otherwise
     * one visitor's session-shaped payload could be publicly cached under a URL
     * every other visitor shares. Every other served dimension (items' mode) is
     * URL-deterministic, so it only needs to be part of the URL, which it is.
     *
     * @param array<string, string> $served context actually served, keyed by query param name
     */
    private function applyCachePolicy(Request $request, JsonResponse $response, array $served): void
    {
        // The UI-locale subscriber touches the session on every request, which
        // would make Symfony force "private" — we own the header explicitly.
        $response->headers->set(AbstractSessionListener::NO_AUTO_CACHE_CONTROL_HEADER, 'true');

        $isExplicit = trim((string) $request->query->get('version', '')) === $served['version']
            && trim((string) $request->query->get('lang', '')) === $served['lang'];

        if ($isExplicit) {
            $response->setPublic();
            $response->setMaxAge(self::PUBLIC_TTL_SECONDS);

            return;
        }

        $response->setPrivate();
        $response->setMaxAge(0);
    }
}
