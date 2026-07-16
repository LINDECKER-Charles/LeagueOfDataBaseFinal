<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\RuneManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class RuneController extends AbstractController
{
    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly ClientManager $clientManager,
        private readonly PageContextResolver $pageContext,
        private readonly RequestStack $requestStack,
        private readonly RuneManager $runeManager,
    ) {}

    /**
     * Liste paginée des arbres de runes. Version/langue depuis la query (URL
     * cacheable), sinon la sélection en session — sans redirect.
     */
    #[Route('/runes', name: 'app_runes', methods: ['GET'])]
    public function runes(): Response
    {
        $ctx = $this->pageContext->listContext(defaultPerPage: 8, maxPerPage: 20);

        try {
            $data = $this->runeManager->paginate($ctx['version'], $ctx['lang'], $ctx['itemPerPage'], $ctx['numPage']);
        } catch (\Throwable $e) {
            return $this->redirectToSetupWithError($ctx, $e);
        }

        $data['meta']['version'] = $ctx['version'];
        $data['meta']['lang']    = $ctx['lang'];

        return $this->render('rune/liste.html.twig', [
            'runesReforgeds' => $data['runesReforgeds'],
            'images'         => $data['images'],
            'meta'           => $data['meta'],
            'client'         => ClientData::fromServices($this->versionManager, $this->clientManager),
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
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
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
