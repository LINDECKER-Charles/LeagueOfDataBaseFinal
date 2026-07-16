<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\SummonerManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class SummonerController extends AbstractController
{
    public function __construct(
        private readonly SummonerManager $summoners,
        private readonly VersionManager $versionManager,
        private readonly ClientManager $clientManager,
        private readonly PageContextResolver $pageContext,
        private readonly RequestStack $requestStack,
    ) {}

    /**
     * Liste des sorts d'invocateur (jeu complet, pas de plafond). Version/langue
     * depuis la query (URL cacheable), sinon la sélection en session — sans redirect.
     */
    #[Route('/summoners', name: 'app_summoners', methods: ['GET'])]
    public function summoners(): Response
    {
        $ctx = $this->pageContext->listContext(defaultPerPage: 8, maxPerPage: 0);

        try {
            $data = $this->summoners->paginate($ctx['version'], $ctx['lang'], $ctx['itemPerPage'], $ctx['numPage']);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($ctx, $e);
        }

        $data['meta']['version'] = $ctx['version'];
        $data['meta']['lang']    = $ctx['lang'];

        return $this->render('summoner/liste.html.twig', [
            'summoners' => $data['summoners'],
            'images'    => $data['images'],
            'meta'      => $data['meta'],
            'client'    => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    /**
     * Détail d'un sort d'invocateur. Version/langue résolues depuis la query, sinon la session.
     */
    #[Route('/summoner/{name}', name: 'app_summoner', methods: ['GET'])]
    public function summoner(string $name): Response
    {
        $sel = $this->pageContext->selection();

        try {
            $image    = $this->summoners->getImage($name . '.png', $sel['version'], [], false, $sel['lang']);
            $summoner = $this->summoners->getByName($name, $sel['version'], $sel['lang']);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($sel, $e);
        }

        return $this->render('summoner/detail.html.twig', [
            'summoner' => $summoner,
            'image'    => $image,
            'client'   => ClientData::fromServices($this->versionManager, $this->clientManager),
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

    /**
     * @param array{version?:string, lang?:string} $ctx
     */
    private function redirectToSetupWithError(array $ctx, \Throwable $e): Response
    {
        $this->requestStack->getSession()->getFlashBag()->clear();
        $this->addFlash('error', $this->dataError($ctx, $e));

        return $this->redirectToRoute('app_setup');
    }

    /**
     * @param array{version?:string, lang?:string} $ctx
     */
    private function dataError(array $ctx, \Throwable $e): string
    {
        return sprintf(
            'Donnés absente sur la version %s et la langue %s Message --> %s',
            $ctx['version'] ?? 'n/a',
            $ctx['lang'] ?? 'n/a',
            $e->getMessage()
        );
    }
}
