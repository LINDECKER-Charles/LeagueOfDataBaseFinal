<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\SummonerManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;

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
     * Liste des sorts d'invocateur (jeu complet, pas de plafond). Version/langue
     * depuis la query (URL cacheable), sinon la sélection en session — sans redirect.
     */
    #[Route('/summoners', name: 'app_summoners', methods: ['GET'])]
    #[Route('/{version}/summoners', name: 'app_summoners_versioned', requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT], methods: ['GET'])]
    public function summoners(): Response
    {
        // Full list in one render — the ResourceFilter island owns search, tag
        // facets and pagination client-side; only version/lang matter server-side.
        $sel = $this->pageContext->selection();

        try {
            $data = $this->summoners->paginate($sel['version'], $sel['lang'], 0, 1);
        } catch (\Throwable $e) {
            return $this->redirectToHomeWithError($sel, $e);
        }

        $data['meta']['version'] = $sel['version'];
        $data['meta']['lang']    = $sel['lang'];

        return $this->render('summoner/liste.html.twig', [
            'summoners' => $data['summoners'],
            'images'    => $data['images'],
            'meta'      => $data['meta'],
            'client'    => $this->clientData(),
        ]);
    }

    /**
     * Détail d'un sort d'invocateur. Version/langue résolues depuis la query, sinon la session.
     */
    #[Route('/summoner/{name}', name: 'app_summoner', methods: ['GET'])]
    #[Route('/{version}/summoner/{name}', name: 'app_summoner_versioned', requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT], methods: ['GET'])]
    public function summoner(string $name): Response
    {
        $sel = $this->pageContext->selection();

        try {
            // Lookup first: an unknown slug must 404 from the dataset alone,
            // without ever asking the CDN for an image that cannot exist.
            $summoner = $this->summoners->getByName($name, $sel['version'], $sel['lang']);
            $image    = $this->summoners->getImage($sel['version'], $name . '.png');
        } catch (\Throwable $e) {
            return $this->detailFailure($sel, $e);
        }

        return $this->render('summoner/detail.html.twig', [
            'summoner' => $summoner,
            'image'    => $image,
            'version'  => $sel['version'],
            'lang'     => $sel['lang'],
            'nav'      => $this->neighbors($this->summoners, $sel['version'], $sel['lang'], $name),
            'client'   => $this->clientData(),
        ]);
    }

    /**
     * API : recherche de sorts d'invocateur par nom → JSON simplifié {id, name, image}.
     */
    #[Route('/api/summoners/search/{name}', name: 'api_summoners_search', methods: ['GET'])]
    public function searchSummonersApi(string $name): JsonResponse
    {
        $session = $this->clientManager->getSession();
        try {
            $summoners = $this->summoners->searchByName($name, $session['version'], $session['lang'], 20);
        } catch (\Throwable $e) {
            return $this->json($this->dataError($session, $e));
        }

        if (empty($summoners)) {
            return $this->json([]);
        }

        $images = $this->summoners->getImages($session['version'], $session['lang'], false, $summoners);

        $final = array_map(static function ($summoner) use ($images) {
            $id = $summoner['id'] ?? null;
            return [
                'id'    => $id,
                'name'  => $summoner['name'] ?? '',
                'image' => $id && isset($images[$id]) ? $images[$id] : null,
            ];
        }, $summoners);

        return $this->json($final);
    }
}
