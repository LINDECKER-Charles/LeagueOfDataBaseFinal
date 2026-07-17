<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\Build;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Repository\BuildVoteRepository;
use App\Service\Build\BuildViewAssembler;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Public build page, keyed by share token — deliberately OUTSIDE ^/builds so
 * the ROLE_USER access_control never applies.
 *
 * Design choice: the page is reachable by ANYONE holding the link, even when
 * the build is private. The unguessable token (12 random bytes) IS the
 * capability — "unlisted" semantics, like a private video link. `isPublic`
 * only governs discovery surfaces (public profile listings), never this page;
 * revocation is {@see \App\Entity\Build::regenerateShareToken()}.
 *
 * Path note: the natural "/build/{token}" is unreachable — nginx serves
 * /build/* as the static Vite output directory (public/build) and 404s
 * anything else before PHP. Hence the short share path /b/{token}; the route
 * NAME stays the stable contract every link is generated from.
 */
final class BuildShareController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly BuildRepository $builds,
        private readonly BuildVoteRepository $votes,
        private readonly BuildViewAssembler $assembler,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/b/{token}', name: 'app_build_show', requirements: ['token' => '[a-f0-9]{24}'], methods: ['GET'])]
    public function show(string $token): Response
    {
        $build = $this->builds->findOneByShareToken($token);
        if ($build === null) {
            throw $this->createNotFoundException('Unknown share token.');
        }

        $sel = $this->pageContext->selection();
        // Names and images resolve on the build's OWN patch, not the visitor's:
        // a shared build must render what its author saw. Language stays the
        // visitor's; the current version only feeds the informative patch chip.
        $version = BuildViewAssembler::renderVersion($build, $sel['version']);

        return $this->render('build/show.html.twig', [
            'client' => $this->clientData(),
            'build' => $build,
            'vm' => $this->assembler->assemble($build, $version, $sel['version'], $sel['lang']),
            'version' => $version,
            'currentVersion' => $sel['version'],
            'lang' => $sel['lang'],
        ] + $this->voteContext($build));
    }

    /**
     * Vote widget context — only PUBLIC builds are votable (an unlisted link
     * must not become a scoring surface), so private pages get no widget.
     *
     * @return array{voteScore: ?int, myVote: int}
     */
    private function voteContext(Build $build): array
    {
        if (!$build->isPublic()) {
            return ['voteScore' => null, 'myVote' => 0];
        }

        $id = (int) $build->getId();
        $user = $this->getUser();

        return [
            'voteScore' => $this->votes->scoreFor([$id])[$id] ?? 0,
            'myVote' => $user instanceof User
                ? ($this->votes->findOneByBuildAndVoter($build, $user)?->getValue() ?? 0)
                : 0,
        ];
    }
}
