<?php
declare(strict_types=1);

namespace App\Service\API\Edition;

/**
 * Which game an item or summoner spell belongs to. Since "League of Legends
 * Classic" (Data Dragon's JADE mode, map 453 "Classic Rift") shipped, the
 * current item.json / summoner.json carry a classic-era twin of many entries
 * under the SAME display name — "Faerie Charm" is both 1004 (current) and
 * 771004 (classic). The edition is the dimension that tells them apart
 * everywhere a name alone used to be enough.
 *
 * The enum values are public contract: rendered as `data-edition` on the list
 * cards (read by the ResourceFilter island) and exposed by the search APIs.
 */
enum Edition: string
{
    case Modern = 'modern';
    case Classic = 'classic';
}
