<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\Build;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Repository\BuildRepository;
use App\Repository\UserRepository;
use App\Service\Picker\PickerCatalog;
use App\Service\Profile\FavoriteSlots;
use App\Service\Profile\ProfilePresenter;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Public "summoner card" — /u/{username}. Opt-in only: a private or unknown
 * profile answers the exact same 404, so the route is no account-existence
 * oracle.
 */
final class PublicProfileController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly UserRepository $users,
        private readonly BuildRepository $builds,
        private readonly FavoriteSlots $favoriteSlots,
        private readonly PickerCatalog $catalog,
        private readonly ProfilePresenter $presenter,
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
        if ($user === null || !$user->isPublicProfile()) {
            throw $this->createNotFoundException('Profile not found.');
        }

        ['version' => $version, 'lang' => $lang] = $this->pageContext->selection();

        return $this->render('profile/public.html.twig', [
            'client' => $this->clientData(),
            'profileUser' => $user,
            'memberSince' => $this->presenter->memberSince($user->getCreatedAt(), $request->getLocale()),
            'favorites' => $this->favoriteSlots->resolveAll($user, $version, $lang),
            'builds' => $this->buildCards($this->builds->findPublicByOwner($user), $version, $lang),
        ]);
    }

    /**
     * @param list<Build> $builds
     * @return list<array<string, mixed>>
     */
    private function buildCards(array $builds, string $version, string $lang): array
    {
        $cards = [];
        foreach ($builds as $build) {
            $champion = $this->tryResolveChampion($build->getChampionId(), $version, $lang);
            $cards[] = [
                'name' => $build->getName(),
                'championId' => $build->getChampionId(),
                'championName' => $champion['name'] ?? null,
                'championImage' => $champion['image'] ?? null,
                'gameVersion' => $build->getGameVersion(),
                'shareToken' => $build->getShareToken(),
                'updatedAt' => $build->getUpdatedAt(),
            ];
        }

        return $cards;
    }

    /** @return ?array{id: string, name: string, image: ?string, type: string} */
    private function tryResolveChampion(string $championId, string $version, string $lang): ?array
    {
        try {
            return $this->catalog->resolveChampion($championId, $version, $lang);
        } catch (\Throwable) {
            return null; // the portrait is decorative — the card degrades to initials
        }
    }
}
