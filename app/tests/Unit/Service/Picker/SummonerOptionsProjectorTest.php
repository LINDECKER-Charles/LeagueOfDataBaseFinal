<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\SummonerOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * Summoner options only offer CLASSIC-mode spells; images are id-keyed;
 * resolution stays presence-based (a stored ARAM favorite still displays).
 */
final class SummonerOptionsProjectorTest extends TestCase
{
    private SummonerOptionsProjector $projector;

    protected function setUp(): void
    {
        $this->projector = new SummonerOptionsProjector();
    }

    /** @return array<string, array<string, mixed>> */
    private function data(): array
    {
        return [
            'SummonerFlash' => [
                'id' => 'SummonerFlash', 'key' => '4', 'name' => 'Saut éclair',
                'modes' => ['CLASSIC', 'ARAM'], 'image' => ['full' => 'SummonerFlash.png'],
            ],
            'SummonerSnowball' => [
                'id' => 'SummonerSnowball', 'key' => '32', 'name' => 'Boule de neige',
                'modes' => ['ARAM'], 'image' => ['full' => 'SummonerSnowball.png'],
            ],
            'SummonerDot' => [
                'id' => 'SummonerDot', 'key' => '14', 'name' => 'Embrasement',
                'modes' => ['CLASSIC'], 'image' => ['full' => 'SummonerDot.png'],
            ],
        ];
    }

    public function testProjectFiltersToClassicAndSortsByName(): void
    {
        $options = $this->projector->project($this->data(), ['SummonerDot' => 'cdn/blobs/dot.png']);

        self::assertSame(['SummonerDot', 'SummonerFlash'], array_column($options, 'id'), 'ARAM-only spell excluded, name order');
        self::assertSame(
            ['SummonerDot' => '/cdn/blobs/dot.png', 'SummonerFlash' => null],
            array_column($options, 'image', 'id'),
            'missing image degrades to null',
        );
        self::assertSame('4', array_column($options, 'key', 'id')['SummonerFlash']);
    }

    public function testResolveIsPresenceBasedNotModeFiltered(): void
    {
        $resolved = $this->projector->resolve($this->data(), ['SummonerSnowball' => 'cdn/blobs/snow.png'], 'SummonerSnowball');

        self::assertSame(
            ['id' => 'SummonerSnowball', 'name' => 'Boule de neige', 'image' => '/cdn/blobs/snow.png', 'type' => 'summoner'],
            $resolved,
        );
        self::assertNull($this->projector->resolve($this->data(), [], 'SummonerNope'));
    }
}
