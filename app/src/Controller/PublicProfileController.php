<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Repository\UserRepository;
use App\Service\Profile\ProfileVersionResolver;
use App\Service\Profile\PublicProfileView;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Public "summoner card" — /u/{username}. Opt-in only: a private or unknown
 * profile answers the exact same 404, so the route is no account-existence
 * oracle. The render model is built by {@see PublicProfileView}, shared with the
 * owner's /profile/preview so both surfaces stay byte-for-byte identical.
 */
final class PublicProfileController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly UserRepository $users,
        private readonly PublicProfileView $view,
        private readonly ProfileVersionResolver $profileVersion,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route(
        '/u/{username}',
        name: 'app_profile_public',
        requirements: ['username' => '[A-Za-z0-9][A-Za-z0-9_.\-]{2,23}'],
        methods: ['GET'],
    )]
    public function show(string $username, Request $request): Response
    {
        $user = $this->users->findOneByUsernameInsensitive($username);
        // A banned owner answers the same 404 as a private/unknown profile —
        // no oracle distinguishing "banned" from "does not exist".
        if ($user === null || !$user->isPublicProfile() || $user->isBanned()) {
            throw $this->createNotFoundException('Profile not found.');
        }

        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();
        // Show the owner's curated patch, so their public card is stable
        // regardless of the visitor's own browsing version.
        $version = $this->profileVersion->effective($user, $version);

        return $this->render('profile/public.html.twig', [
            'client' => $this->clientData(),
        ] + $this->view->build($user, $version, $lang, $request->getLocale()));
    }
}
