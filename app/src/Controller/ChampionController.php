<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\ChampionManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

final class ChampionController extends AbstractController
{
    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly ClientManager $clientManager,
        private readonly PageContextResolver $pageContext,
        private readonly RequestStack $requestStack,
        private readonly ChampionManager $championManager,
    ) {}

    /**
     * Liste paginée des champions. Version/langue depuis la query (URL cacheable),
     * sinon la sélection en session — sans redirect.
     */
    #[Route('/champions', name: 'app_champions', methods: ['GET'])]
    public function champions(): Response
    {
        $ctx = $this->pageContext->listContext(defaultPerPage: 20, maxPerPage: 20);

        try {
            $data = $this->championManager->paginate($ctx['version'], $ctx['lang'], $ctx['itemPerPage'], $ctx['numPage']);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($ctx, $e);
        }

        $data['meta']['version'] = $ctx['version'];
        $data['meta']['lang']    = $ctx['lang'];

        return $this->render('champion/liste.html.twig', [
            'champions' => $data['champions'],
            'images'    => $data['images'],
            'meta'      => $data['meta'],
            'client'    => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    /**
     * Détail d'un champion. Version/langue résolues depuis la query, sinon la session.
     */
    #[Route('/champion/{name}', name: 'app_champion', methods: ['GET'])]
    public function champion(string $name): Response
    {
        $sel = $this->pageContext->selection();

        try {
            $image    = $this->championManager->getImage($name . '.png', $sel['version'], [], false, $sel['lang']);
            $champion = $this->championManager->getByName($name, $sel['version'], $sel['lang']);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($sel, $e);
        }

        return $this->render('champion/detail.html.twig', [
            'champion' => $champion,
            'image'    => $image,
            'client'   => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    /**
     * API : recherche de champions par nom → JSON simplifié {id, name, image}.
     */
    #[Route('/api/champions/search/{name}', name: 'api_champions_search', methods: ['GET'])]
    public function searchChampionsApi(string $name): JsonResponse
    {
        $session = $this->clientManager->getSession();
        try {
            $champions = $this->championManager->searchByName($name, $session['version'], $session['lang'], 20);
        } catch (\Throwable $e) {
            return $this->json($this->dataError($session, $e));
        }

        if (empty($champions)) {
            return $this->json([]);
        }

        $images = $this->championManager->getImages($session['version'], $session['lang'], false, $champions);

        $final = array_map(static fn ($champion, $image) => [
            'id'    => $champion['id'] ?? null,
            'name'  => $champion['name'] ?? '',
            'image' => $image,
        ], $champions, $images);

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
