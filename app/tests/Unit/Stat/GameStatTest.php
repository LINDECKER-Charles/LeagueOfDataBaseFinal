<?php
declare(strict_types=1);

namespace App\Tests\Unit\Stat;

use App\Stat\GameStat;
use PHPUnit\Framework\TestCase;

final class GameStatTest extends TestCase
{
    public function testBundledStatExposesIconPath(): void
    {
        self::assertSame('/icons/stats/armor.png', GameStat::Armor->icon());
        self::assertSame('/icons/stats/attack_damage.png', GameStat::AttackDamage->icon());
    }

    public function testUnbundledStatsHaveNoIconYet(): void
    {
        self::assertNull(GameStat::Mana->icon());
        self::assertNull(GameStat::CritChance->icon());
        self::assertNull(GameStat::LifeSteal->icon());
        self::assertNull(GameStat::AttackRange->icon());
    }

    public function testLabelKeyIsNamespaced(): void
    {
        self::assertSame('stat.attack_damage', GameStat::AttackDamage->labelKey());
        self::assertSame('stat.magic_resist', GameStat::MagicResist->labelKey());
    }

    public function testFromItemStatsKeepsCatalogueOrderAndFlagsPercents(): void
    {
        // Deliberately out of catalogue order in the input map.
        $rows = GameStat::fromItemStats([
            'FlatCritChanceMod'     => 0.25,
            'PercentAttackSpeedMod' => 0.25,
            'FlatPhysicalDamageMod' => 75,
        ]);

        self::assertSame(
            [GameStat::AttackDamage, GameStat::AttackSpeed, GameStat::CritChance],
            array_column($rows, 'stat'),
        );
        self::assertSame([false, true, true], array_column($rows, 'percent'));
        self::assertSame([75.0, 0.25, 0.25], array_column($rows, 'value'));
    }

    public function testFromItemStatsSkipsZeroAndUnknownKeys(): void
    {
        $rows = GameStat::fromItemStats([
            'FlatArmorMod'     => 0,        // zero → skipped
            'FlatLifestealMod' => 10,       // not a real DDragon key → ignored
            'FlatHPPoolMod'    => 300,
        ]);

        self::assertCount(1, $rows);
        self::assertSame(GameStat::Health, $rows[0]['stat']);
    }

    public function testFromItemStatsEmptyInputs(): void
    {
        self::assertSame([], GameStat::fromItemStats(null));
        self::assertSame([], GameStat::fromItemStats([]));
    }
}
