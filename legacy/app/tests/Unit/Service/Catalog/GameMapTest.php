<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Catalog;

use App\Service\Catalog\GameMap;
use App\Service\Picker\GameMode;
use PHPUnit\Framework\TestCase;

/**
 * One owner of the Data Dragon map ids: the build modes resolve theirs here,
 * and an item's availability is read off its `maps` flags — never 453 (an
 * edition, not a map) nor 22 (TFT, never true).
 */
final class GameMapTest extends TestCase
{
    public function testBuildModesResolveTheirMapHere(): void
    {
        self::assertSame('11', GameMode::SummonersRift->mapId());
        self::assertSame(GameMap::HowlingAbyss, GameMode::Aram->map());
        self::assertSame('30', GameMode::Arena->mapId());
    }

    public function testAvailabilityReadsTheTrueFlagsOnly(): void
    {
        $maps = GameMap::availableOn(['maps' => [
            '11' => true, '12' => false, '21' => true, '22' => true, '35' => true, '453' => true,
        ]]);

        self::assertSame(
            [GameMap::SummonersRift, GameMap::NexusBlitz, GameMap::Brawl],
            $maps,
        );
    }

    public function testAnEntryWithoutMapsIsAvailableNowhere(): void
    {
        self::assertSame([], GameMap::availableOn(['name' => 'Boots']));
        self::assertSame([], GameMap::availableOn(['maps' => 'broken']));
    }

    public function testEveryMapHasATranslationKey(): void
    {
        foreach (GameMap::cases() as $map) {
            self::assertSame('map.'.$map->value, $map->labelKey());
        }
    }
}
