<?php
declare(strict_types=1);

namespace App\Controller\Resource;

use App\Service\API\ChampionManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Profile\ChampionSkins;
use Psr\Log\LoggerInterface;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

final class ChampionController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        LoggerInterface $catalogLogger,
        private readonly ChampionManager $championManager,
        private readonly ChampionSkins $championSkins,
    ) {
        parent::__construct(
            $versionManager,
            $clientManager,
            $pageContext,
            $requestStack,
            $catalogLogger,
        );
    }

    /**
     * Paginated champion list. Version/lang come from the query (cacheable URL),
     * otherwise from the session selection — without any redirect.
     */
    #[Route('/champions', name: 'app_champions', methods: ['GET'])]
    #[Route(
        '/{version}/champions',
        name: 'app_champions_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET']
    )]
    public function champions(): Response
    {
        return $this->listPage($this->championManager, 'champion/liste.html.twig');
    }

    /**
     * Champion detail. Version/lang resolved from the query, otherwise the session.
     */
    #[Route('/champion/{name}', name: 'app_champion', methods: ['GET'])]
    #[Route(
        '/{version}/champion/{name}',
        name: 'app_champion_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET']
    )]
    public function champion(string $name): Response
    {
        $selection = $this->pageContext->selection();
        ['version' => $version, 'lang' => $lang] = $selection;

        try {
            $summary = $this->lookupSummary($name, $version, $lang);
        } catch (\Throwable $e) {
            return $this->detailFailure($selection, $e, 'app_champions_versioned');
        }

        $detail   = $this->fullDetail($name, $version, $lang);
        $champion = array_merge($summary['champion'], $detail['detail']);
        $chromas  = $this->chromas($champion, $version);
        $champion = $this->withoutChromaSkins($champion, $chromas);

        return $this->render('champion/detail.html.twig', [
            'champion'      => $champion,
            'image'         => $summary['image'],
            // Hero art URLs come from the one owner of the CDN casing quirks
            // (centered/FiddleSticks) rather than being rebuilt in Twig.
            'heroArt'       => $this->championSkins->championArt(
                (string) ($champion['id'] ?? $name),
            ),
            'abilityImages' => $detail['abilityImages'],
            'chromas'       => $chromas,
            'version'       => $version,
            'lang'          => $lang,
            'nav'           => $this->neighbors($this->championManager, $selection, $name),
            'client'        => $this->clientData(),
        ]);
    }

    /**
     * API: champion search by name → simplified JSON {id, name, image}.
     */
    #[Route('/api/champions/search/{name}', name: 'api_champions_search', methods: ['GET'])]
    public function searchChampionsApi(string $name): JsonResponse
    {
        return $this->searchResponse(
            $this->championManager,
            $name,
            // ChampionManager::getImages keys its map by champion ID.
            static fn (array $rows, array $images): array => array_map(
                static fn (array $row): ?string => $images[(string) ($row['id'] ?? '')] ?? null,
                $rows,
            ),
        );
    }

    /**
     * The two reads a detail page cannot render without. Lookup first: an unknown
     * slug must 404 from the dataset alone, without ever asking the CDN for an
     * image that cannot exist. Any failure here decides the page's HTTP outcome.
     *
     * @return array{champion: array<string, mixed>, image: mixed}
     */
    private function lookupSummary(string $name, string $version, string $lang): array
    {
        return [
            'champion' => $this->championManager->getByName($name, $version, $lang),
            'image'    => $this->championManager->getImage($version, $name . '.png'),
        ];
    }

    /**
     * The full detail (spells, skins, lore, tips) and its ability icons —
     * best-effort: if the heavier payload or its icons fail, the page still
     * renders on the summary alone rather than breaking.
     *
     * @return array{detail: array<string, mixed>, abilityImages: array<mixed>}
     */
    private function fullDetail(string $name, string $version, string $lang): array
    {
        $detail = [];

        try {
            $detail = $this->championManager->getDetail($name, $version, $lang);

            return $detail === [] ? ['detail' => [], 'abilityImages' => []] : [
                'detail'        => $detail,
                'abilityImages' => $this->championManager->getAbilityImages($detail, $version),
            ];
        } catch (\Throwable) {
            // Degrade silently to whatever was already fetched — the page must not break.
            return ['detail' => $detail, 'abilityImages' => []];
        }
    }

    /**
     * Chroma metadata (CommunityDragon) — purely cosmetic, keyed by skin id.
     * Isolated so an upstream hiccup never costs the rest of the page.
     *
     * @param array<string, mixed> $champion
     * @return array<mixed>
     */
    private function chromas(array $champion, string $version): array
    {
        $key = (string) ($champion['key'] ?? '');
        if ($key === '') {
            return [];
        }

        try {
            return $this->championManager->getChromas($key, $version);
        } catch (\Throwable) {
            // No chromas rendered — the skins still show.
            return [];
        }
    }

    /**
     * Data Dragon inlines chromas as standalone skins (no splash) — surface them
     * only through the ChromaStrip, never as skin tiles.
     *
     * @param array<string, mixed> $champion
     * @param array<mixed> $chromas
     * @return array<string, mixed>
     */
    private function withoutChromaSkins(array $champion, array $chromas): array
    {
        if (!isset($champion['skins']) || !is_array($champion['skins'])) {
            return $champion;
        }

        $champion['skins'] = $this->championManager->withoutChromaSkins(
            $champion['skins'],
            $chromas
        );

        return $champion;
    }

}
