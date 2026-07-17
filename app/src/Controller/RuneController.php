<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\RuneManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\RequestStack;

final class RuneController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly RuneManager $runeManager,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    /**
     * Liste paginée des arbres de runes. Version/langue depuis la query (URL
     * cacheable), sinon la sélection en session — sans redirect.
     */
    #[Route('/runes', name: 'app_runes', methods: ['GET'])]
    public function runes(): Response
    {
        $ctx = $this->pageContext->listContext(defaultPerPage: 8, maxPerPage: 20);

        try {
            // Full list in one render — the ResourceFilter island owns search
            // and pagination client-side (rune trees are only a handful).
            $data = $this->runeManager->paginate($ctx['version'], $ctx['lang'], 0, 1);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($ctx, $e);
        }

        $data['meta']['version'] = $ctx['version'];
        $data['meta']['lang']    = $ctx['lang'];

        return $this->render('rune/liste.html.twig', [
            'runesReforgeds' => $data['runesReforgeds'],
            'images'         => $data['images'],
            'meta'           => $data['meta'],
            'client'         => $this->clientData(),
        ]);
    }

    /**
     * Détail d'un arbre de runes. Version/langue résolues depuis la query, sinon la session.
     */
    #[Route('/rune/{name}', name: 'app_rune', methods: ['GET'])]
    public function rune(string $name): Response
    {
        $sel = $this->pageContext->selection();

        try {
            $rune   = $this->runeManager->getByName($name, $sel['version'], $sel['lang']);
            $images = $this->runeManager->getImages($sel['version'], $sel['lang'], false, [$rune]);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($sel, $e);
        }

        return $this->render('rune/detail.html.twig', [
            'rune'   => $rune,
            'images' => $images,
            'client' => $this->clientData(),
        ]);
    }
}
