<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Twig\StatExtension;
use PHPUnit\Framework\TestCase;

final class StatExtensionTest extends TestCase
{
    private StatExtension $extension;

    protected function setUp(): void
    {
        $this->extension = new StatExtension();
    }

    public function testIconUrlResolvesBundledStatsAndNullsTheRest(): void
    {
        self::assertSame('/icons/stats/armor.png', $this->extension->iconUrl('armor'));
        self::assertNull($this->extension->iconUrl('mana'));
        self::assertNull($this->extension->iconUrl('not_a_stat'));
    }

    public function testItemStatsFormatsFlatsAndPercents(): void
    {
        $rows = $this->extension->itemStats([
            'FlatPhysicalDamageMod' => 75,
            'PercentAttackSpeedMod' => 0.25,
            'PercentLifeStealMod'   => 0.1,
        ]);

        self::assertSame([
            ['slug' => 'attack_damage', 'label' => 'stat.attack_damage', 'value' => '+75'],
            ['slug' => 'attack_speed', 'label' => 'stat.attack_speed', 'value' => '+25 %'],
            ['slug' => 'life_steal', 'label' => 'stat.life_steal', 'value' => '+10 %'],
        ], $rows);
    }

    public function testItemStatsEmptyForNullOrEmpty(): void
    {
        self::assertSame([], $this->extension->itemStats(null));
        self::assertSame([], $this->extension->itemStats([]));
    }
}
