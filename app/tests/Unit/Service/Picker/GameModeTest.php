<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\GameMode;
use PHPUnit\Framework\TestCase;

/**
 * The enum values are persisted (builds.game_mode) and its map ids drive item
 * availability — both mappings are frozen contracts.
 */
final class GameModeTest extends TestCase
{
    public function testMapIdMappingIsTheDdragonContract(): void
    {
        self::assertSame('11', GameMode::SummonersRift->mapId());
        self::assertSame('12', GameMode::Aram->mapId());
        self::assertSame('21', GameMode::NexusBlitz->mapId());
        self::assertSame('30', GameMode::Arena->mapId());
    }

    public function testPersistedValuesAreStable(): void
    {
        self::assertSame(
            ['sr', 'aram', 'nexus_blitz', 'arena'],
            array_map(static fn (GameMode $m): string => $m->value, GameMode::cases()),
        );
        self::assertSame(GameMode::SummonersRift, GameMode::DEFAULT);
    }

    public function testMapIdsAreDistinct(): void
    {
        $mapIds = array_map(static fn (GameMode $m): string => $m->mapId(), GameMode::cases());

        self::assertSame($mapIds, array_values(array_unique($mapIds)));
    }

    public function testLabelKeysFollowTheTranslationNamespace(): void
    {
        foreach (GameMode::cases() as $mode) {
            self::assertSame('build.mode.'.$mode->value, $mode->labelKey());
        }
    }

    public function testFromFormDefaultsBlankAndRejectsUnknown(): void
    {
        self::assertSame(GameMode::DEFAULT, GameMode::fromForm(null));
        self::assertSame(GameMode::DEFAULT, GameMode::fromForm(''));
        self::assertSame(GameMode::DEFAULT, GameMode::fromForm('  '));
        self::assertSame(GameMode::Aram, GameMode::fromForm('aram'));
        self::assertSame(GameMode::Aram, GameMode::fromForm(' aram '));
        self::assertNull(GameMode::fromForm('urf'));
    }
}
