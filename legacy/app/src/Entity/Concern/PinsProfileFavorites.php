<?php
declare(strict_types=1);

namespace App\Entity\Concern;

use Doctrine\ORM\Mapping as ORM;

/**
 * The four favourite picks plus the profile banner, and the Data Dragon patch
 * they are pinned to. Grouped because the pin is what keeps the picks readable:
 * they are always resolved against `preferredVersion`, never in isolation.
 */
trait PinsProfileFavorites
{
    #[ORM\Column(length: 64, nullable: true)]
    private ?string $favoriteChampionId = null;

    #[ORM\Column(length: 16, nullable: true)]
    private ?string $favoriteItemId = null;

    #[ORM\Column(length: 16, nullable: true)]
    private ?string $favoriteRuneId = null;

    #[ORM\Column(length: 64, nullable: true)]
    private ?string $favoriteSummonerId = null;

    /**
     * Favorite skin as the profile banner. Stored as "{championId}_{skinNum}"
     * (e.g. "Ahri_7") — the DDragon splash filename stem, so the banner URL
     * derives without any data-layer lookup.
     */
    #[ORM\Column(length: 64, nullable: true)]
    private ?string $favoriteSkinId = null;

    /**
     * Data Dragon patch the profile pins its favorites to, so a favorite never
     * silently disappears (nor gets wiped on save) when the browsing version
     * lacks it. Null = follow the current browsing version.
     */
    #[ORM\Column(length: 24, nullable: true)]
    private ?string $preferredVersion = null;

    public function getFavoriteChampionId(): ?string
    {
        return $this->favoriteChampionId;
    }

    public function setFavoriteChampionId(?string $favoriteChampionId): static
    {
        $this->favoriteChampionId = $favoriteChampionId;

        return $this;
    }

    public function getFavoriteItemId(): ?string
    {
        return $this->favoriteItemId;
    }

    public function setFavoriteItemId(?string $favoriteItemId): static
    {
        $this->favoriteItemId = $favoriteItemId;

        return $this;
    }

    public function getFavoriteRuneId(): ?string
    {
        return $this->favoriteRuneId;
    }

    public function setFavoriteRuneId(?string $favoriteRuneId): static
    {
        $this->favoriteRuneId = $favoriteRuneId;

        return $this;
    }

    public function getFavoriteSummonerId(): ?string
    {
        return $this->favoriteSummonerId;
    }

    public function setFavoriteSummonerId(?string $favoriteSummonerId): static
    {
        $this->favoriteSummonerId = $favoriteSummonerId;

        return $this;
    }

    public function getFavoriteSkinId(): ?string
    {
        return $this->favoriteSkinId;
    }

    public function setFavoriteSkinId(?string $favoriteSkinId): static
    {
        $this->favoriteSkinId = $favoriteSkinId;

        return $this;
    }

    public function getPreferredVersion(): ?string
    {
        return $this->preferredVersion;
    }

    public function setPreferredVersion(?string $preferredVersion): static
    {
        $this->preferredVersion = $preferredVersion;

        return $this;
    }
}
