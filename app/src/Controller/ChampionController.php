<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\ChampionManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

final class ChampionController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly ChampionManager $championManager,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

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
            'client'    => $this->clientData(),
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

        // The full detail (spells, skins, lore, tips) is best-effort: if the
        // heavier payload or its icons fail, the page still renders on the summary.
        $abilityImages = [];
        try {
            $detail = $this->championManager->getDetail($name, $sel['version'], $sel['lang']);
            if ($detail !== []) {
                $champion = array_merge($champion, $detail);
                $abilityImages = $this->championManager->getAbilityImages($detail, $sel['version']);
            }
        } catch (\Throwable) {
            // Degrade silently to the summary — the page must not break.
        }

        // Chroma metadata (CommunityDragon) — purely cosmetic, keyed by skin id.
        // Isolated so an upstream hiccup never costs the rest of the page.
        $chromas = [];
        try {
            if (($key = (string) ($champion['key'] ?? '')) !== '') {
                $chromas = $this->championManager->getChromas($key, $sel['version']);
            }
        } catch (\Throwable) {
            // No chromas rendered — the skins still show.
        }

        return $this->render('champion/detail.html.twig', [
            'champion'      => $champion,
            'image'         => $image,
            'abilityImages' => $abilityImages,
            'chromas'       => $chromas,
            'version'       => $sel['version'],
            'client'        => $this->clientData(),
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
}
