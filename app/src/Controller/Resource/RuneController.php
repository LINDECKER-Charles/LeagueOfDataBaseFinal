<?php
declare(strict_types=1);

namespace App\Controller\Resource;

use App\Service\API\DatasetRef;
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
     * Paginated list of rune trees. Version/language come from the query string
     * (cacheable URL), otherwise from the session selection — without a redirect.
     */
    #[Route('/runes', name: 'app_runes', methods: ['GET'])]
    #[Route(
        '/{version}/runes',
        name: 'app_runes_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET'],
    )]
    public function runes(): Response
    {
        return $this->listPage($this->runeManager, 'rune/liste.html.twig');
    }

    /**
     * Rune tree detail. Version/language resolved from the query string,
     * otherwise from the session.
     */
    #[Route('/rune/{name}', name: 'app_rune', methods: ['GET'])]
    #[Route(
        '/{version}/rune/{name}',
        name: 'app_rune_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET'],
    )]
    public function rune(string $name): Response
    {
        $selection = $this->pageContext->selection();
        ['version' => $version, 'lang' => $lang] = $selection;
        $dataset   = DatasetRef::fromSelection($selection);

        try {
            $rune   = $this->runeManager->getByName($name, $version, $lang);
            // Detail render resolves images synchronously by default (only the list
            // opts into deferral), so a cold version paints real icons, not placeholders.
            $images = $this->runeManager->getImages($dataset, false, [$rune]);
        } catch (\Throwable $e) {
            return $this->detailFailure($selection, $e, 'app_runes_versioned');
        }

        return $this->render('rune/detail.html.twig', [
            'rune'    => $rune,
            'images'  => $images,
            'version' => $version,
            'lang'    => $lang,
            'nav'     => $this->neighbors($this->runeManager, $selection, $name),
            'client'  => $this->clientData(),
        ]);
    }
}
