<?php
declare(strict_types=1);

namespace App\Service\Profile;

use App\Entity\Build;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Service\Picker\PickerCatalog;

/**
 * Assembles the render model of the public summoner card. Shared by the public
 * route (/u/{username}) and the owner's preview (/profile/preview) so both show
 * the exact same page — the preview is not an approximation, it is the page.
 */
final class PublicProfileView
{
    public function __construct(
        private readonly BuildRepository $builds,
        private readonly FavoriteSlots $favoriteSlots,
        private readonly ChampionSkins $skins,
        private readonly PickerCatalog $catalog,
        private readonly ProfilePresenter $presenter,
    ) {}

    /** @return array<string, mixed> params for profile/public.html.twig (minus `client`) */
    public function build(User $user, string $version, string $lang, string $locale): array
    {
        $skinBanner = $this->skins->resolveBanner($user->getFavoriteSkinId(), $version, $lang);
        $heroBackground = $this->skins->heroBackground($skinBanner, $user->getFavoriteChampionId());

        return [
            'profileUser'    => $user,
            'memberSince'    => $this->presenter->memberSince($user->getCreatedAt(), $locale),
            'favorites'      => $this->favoriteSlots->resolveAll($user, $version, $lang),
            'skinBanner'     => $skinBanner,
            'heroBackground' => $heroBackground,
            'builds'         => $this->buildCards($this->builds->findPublicByOwner($user), $version, $lang),
        ];
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
                'name'          => $build->getName(),
                'championId'    => $build->getChampionId(),
                'championName'  => $champion['name'] ?? null,
                'championImage' => $champion['image'] ?? null,
                'gameVersion'   => $build->getGameVersion(),
                'shareToken'    => $build->getShareToken(),
                'updatedAt'     => $build->getUpdatedAt(),
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
