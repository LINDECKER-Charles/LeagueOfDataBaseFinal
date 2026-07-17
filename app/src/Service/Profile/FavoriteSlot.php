<?php
declare(strict_types=1);

namespace App\Service\Profile;

/**
 * The four favorite slots of a summoner profile — single source of the slot
 * model: its POST/island field name and the length cap of the backing
 * users.favorite_*_id column (Doctrine mapping in {@see \App\Entity\User}).
 */
enum FavoriteSlot: string
{
    case Champion = 'champion';
    case Item = 'item';
    case Rune = 'rune';
    case Summoner = 'summoner';

    public function fieldName(): string
    {
        return match ($this) {
            self::Champion => 'favoriteChampionId',
            self::Item => 'favoriteItemId',
            self::Rune => 'favoriteRuneId',
            self::Summoner => 'favoriteSummonerId',
        };
    }

    public function maxLength(): int
    {
        return match ($this) {
            self::Champion, self::Summoner => 64,
            self::Item, self::Rune => 16,
        };
    }
}
