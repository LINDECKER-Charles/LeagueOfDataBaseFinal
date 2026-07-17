<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\Changelog\ChangelogReader;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Player-facing changelog — the released version history synthesized from the
 * internal technical journal (docs/changelog/) into public/changelog/*.json.
 *
 * Fully server-rendered (SEO + no-JS by design): a near-static editorial page
 * has no need for a Vue island — native <details> drives the per-release
 * disclosure. Extends the resource base only for the transverse `client`
 * view-model the shared header/switcher require.
 */
final class ChangelogController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly ChangelogReader $changelog,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/changelog', name: 'app_changelog', methods: ['GET'])]
    public function index(): Response
    {
        return $this->render('changelog/index.html.twig', [
            'client'   => $this->clientData(),
            'releases' => $this->changelog->releases(),
        ]);
    }
}
