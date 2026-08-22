<?php
declare(strict_types=1);

namespace App\Controller\Resource;

use App\Service\API\DatasetRef;
use App\Service\API\SummonerManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

final class SummonerController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly SummonerManager $summoners,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    /**
     * Full summoner-spell set, no cap. Version/lang come from the query (so the
     * URL stays cacheable), otherwise from the session selection — no redirect.
     */
    #[Route('/summoners', name: 'app_summoners', methods: ['GET'])]
    #[Route(
        '/{version}/summoners',
        name: 'app_summoners_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET'],
    )]
    public function summoners(): Response
    {
        return $this->listPage($this->summoners, 'summoner/liste.html.twig');
    }

    /** Version/lang resolved from the query, otherwise from the session. */
    #[Route('/summoner/{name}', name: 'app_summoner', methods: ['GET'])]
    #[Route(
        '/{version}/summoner/{name}',
        name: 'app_summoner_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET'],
    )]
    public function summoner(string $name): Response
    {
        $selection = $this->pageContext->selection();
        ['version' => $version, 'lang' => $lang] = $selection;

        try {
            // Lookup first: an unknown slug must 404 from the dataset alone,
            // without ever asking the CDN for an image that cannot exist.
            $summoner = $this->summoners->getByName($name, $version, $lang);
            $image    = $this->summoners->getImage($version, $name . '.png');
        } catch (\Throwable $e) {
            return $this->detailFailure($selection, $e, 'app_summoners_versioned');
        }

        return $this->render('summoner/detail.html.twig', [
            'summoner'    => $summoner,
            'image'       => $image,
            'version'     => $version,
            'lang'        => $lang,
            'nav'         => $this->neighbors($this->summoners, $selection, $name),
            // Which game this spell belongs to, and its twin in the other one
            // (SummonerFlash ↔ SummonerFlash_Jade) when the patch carries it.
            'edition'     => $this->summoners->editionOf($name, $summoner)->value,
            'counterpart' => $this->summoners->counterpart(
                $name,
                DatasetRef::fromSelection($selection),
            ),
            'client'      => $this->clientData(),
        ]);
    }

    /** Name search — simplified JSON payload {id, name, image}. */
    #[Route('/api/summoners/search/{name}', name: 'api_summoners_search', methods: ['GET'])]
    public function searchSummonersApi(string $name): JsonResponse
    {
        return $this->searchResponse(
            $this->summoners,
            $name,
            // SummonerManager::getImages keys its map by spell ID.
            static fn (array $rows, array $images): array => array_map(
                static fn (array $row): ?string => $images[$row['id'] ?? ''] ?? null,
                $rows,
            ),
        );
    }
}
