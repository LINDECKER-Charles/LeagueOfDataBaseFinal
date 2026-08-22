<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\Edition\Edition;
use App\Service\API\ItemManager;
use App\Service\API\SummonerManager;

/**
 * A patch carrying League of Legends Classic: the classic twin of an item /
 * spell shares its display name with the current one. The managers must keep
 * the two apart (own icon, own edition, cross-linked twin) — the list used to
 * paint the classic icon on both "Long Sword" cards.
 */
final class ManagerEditionTest extends SeededManagerTestCase
{
    protected function itemData(): array
    {
        return ['type' => 'item', 'data' => [
            '1036'   => ['name' => 'Long Sword', 'image' => ['full' => '1036.png']],
            '771036' => ['name' => 'Long Sword', 'image' => ['full' => '771036.png']],
            // Arena variant: same name too, but outside the twinnable ranges.
            '221036' => ['name' => 'Long Sword', 'image' => ['full' => '221036.png']],
            // Riot reused the id: the classic 3001 is not today's 3001.
            '3001'   => ['name' => 'Evenshroud', 'image' => ['full' => '3001.png']],
            '773001' => ['name' => 'Abyssal Scepter', 'image' => ['full' => '773001.png']],
        ]];
    }

    protected function summonerData(): array
    {
        return ['type' => 'summoner', 'data' => [
            'SummonerFlash' => [
                'id' => 'SummonerFlash', 'name' => 'Flash',
                'image' => ['full' => 'SummonerFlash.png'], 'modes' => ['CLASSIC', 'ARAM'],
            ],
            'SummonerFlash_Jade' => [
                'id' => 'SummonerFlash_Jade', 'name' => 'Flash',
                'image' => ['full' => 'SummonerFlash_Jade.png'], 'modes' => ['JADE'],
            ],
            'SummonerHeal' => [
                'id' => 'SummonerHeal', 'name' => 'Heal',
                'image' => ['full' => 'SummonerHeal.png'], 'modes' => ['CLASSIC'],
            ],
        ]];
    }

    protected function manifests(): array
    {
        return [
            'item' => [
                '1036.png'   => 'cdn/longsword.png',
                '771036.png' => 'cdn/classic-longsword.png',
                '221036.png' => 'cdn/arena-longsword.png',
                '3001.png'   => 'cdn/evenshroud.png',
                '773001.png' => 'cdn/abyssal.png',
            ],
            'summoner' => [
                'SummonerFlash.png'      => 'cdn/flash.png',
                'SummonerFlash_Jade.png' => 'cdn/classic-flash.png',
                'SummonerHeal.png'       => 'cdn/heal.png',
            ],
        ] + parent::manifests();
    }

    /** The regression guard for the root bug: three namesakes, three icons. */
    public function testNamesakeItemsResolveTheirOwnIcons(): void
    {
        $images = $this->manager(ItemManager::class)->getImages($this->dataset());

        self::assertSame([
            1036   => 'cdn/longsword.png',
            771036 => 'cdn/classic-longsword.png',
            221036 => 'cdn/arena-longsword.png',
            3001   => 'cdn/evenshroud.png',
            773001 => 'cdn/abyssal.png',
        ], $images);
    }

    public function testTheItemTwinIsResolvedBothWaysAgainstTheDataset(): void
    {
        $items = $this->manager(ItemManager::class);

        self::assertSame(
            ['id' => '771036', 'name' => 'Long Sword', 'edition' => 'classic'],
            $items->counterpart('1036', $this->dataset()),
        );
        self::assertSame(
            ['id' => '1036', 'name' => 'Long Sword', 'edition' => 'modern'],
            $items->counterpart('771036', $this->dataset()),
        );
        self::assertNull($items->counterpart('221036', $this->dataset()), 'Arena: no twin range');
        self::assertNull(
            $items->counterpart('773001', $this->dataset()),
            'a reused id is not a twin: the current 3001 is another item',
        );
        self::assertNull($items->counterpart('3001', $this->dataset()));
        self::assertSame(Edition::Classic, $items->editionOfId('771036', $this->dataset()));
        self::assertSame(Edition::Modern, $items->editionOfId('9999', $this->dataset()));
    }

    public function testATwinAbsentFromThePatchIsNotLinked(): void
    {
        $summoners = $this->manager(SummonerManager::class);

        self::assertNull($summoners->counterpart('SummonerHeal', $this->dataset()));
        self::assertSame(
            ['id' => 'SummonerFlash', 'name' => 'Flash', 'edition' => 'modern'],
            $summoners->counterpart('SummonerFlash_Jade', $this->dataset()),
        );
    }

    public function testSearchHitsCarryTheirEdition(): void
    {
        $hits = $this->manager(SummonerManager::class)->searchByName('flash', $this->dataset());

        self::assertSame(
            [['SummonerFlash', 'modern'], ['SummonerFlash_Jade', 'classic']],
            array_map(static fn (array $hit): array => [$hit['id'], $hit['edition']], $hits),
        );

        $hits = $this->manager(ItemManager::class)->searchByName('long', $this->dataset());

        self::assertSame(
            [[1036, 'modern'], [771036, 'classic'], [221036, 'modern']],
            array_map(static fn (array $hit): array => [$hit['id'], $hit['edition']], $hits),
        );
    }
}
