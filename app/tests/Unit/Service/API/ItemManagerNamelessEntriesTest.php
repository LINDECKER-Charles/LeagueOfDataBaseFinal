<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\ItemManager;
use App\Service\API\ResourceNotFoundException;

/**
 * Riot ships unnamed debris entries in item.json (empty name in every locale).
 * They are not encyclopedia entries: invisible to every browsing surface —
 * list, search, pager index, counts, images — and their direct URL is an
 * honest 404. The raw map keeps them for recipe/related resolution.
 */
final class ItemManagerNamelessEntriesTest extends SeededManagerTestCase
{
    protected function itemData(): array
    {
        return ['type' => 'item', 'data' => [
            '1036' => ['name' => 'Long Sword', 'image' => ['full' => '1036.png']],
            '2008' => ['name' => '', 'image' => ['full' => '2008.png']],
            '3078' => ['name' => 'Trinity Force', 'image' => ['full' => '3078.png']],
            '7050' => ['name' => 'Gangplank Placeholder', 'image' => ['full' => '7050.png']],
            // 3901-3903 ship marked-up copy as their name (16.16.1).
            '3901' => [
                'name' => '<rarityLegendary>Feu à volonté</rarityLegendary>'
                    .'<br><subtitleLeft><silver>500 serpents</silver></subtitleLeft>',
                'image' => ['full' => '3901.png'],
            ],
        ]];
    }

    protected function manifests(): array
    {
        return ['item' => [
            '1036.png' => 'cdn/longsword.png',
            '3078.png' => 'cdn/trinity.png',
            '3901.png' => 'cdn/gp-fire.png',
        ]] + parent::manifests();
    }

    public function testDebrisEntriesAreInvisibleToEveryBrowsingSurface(): void
    {
        $items = $this->manager(ItemManager::class);
        $page  = $items->paginate($this->dataset(), 0);

        self::assertSame([1036, 3078, 3901], array_keys($page['items']));
        self::assertSame(3, $page['meta']['totalItem']);
        // PHP recasts the numeric-string index keys to int, as everywhere else.
        self::assertSame(
            [1036, 3078, 3901],
            array_keys($items->listIndex(self::VERSION, self::LANG)),
        );
    }

    /** The name proper survives; the markup and the price subtitle do not. */
    public function testAMarkedUpNameIsReducedToTheNameProper(): void
    {
        $items = $this->manager(ItemManager::class);

        self::assertSame(
            'Feu à volonté',
            $items->getByName('3901', self::VERSION, self::LANG)['name'],
        );
        self::assertSame(
            'Feu à volonté',
            $items->listIndex(self::VERSION, self::LANG)[3901],
        );
    }

    public function testANamelessIdIsAnHonest404(): void
    {
        $this->expectException(ResourceNotFoundException::class);

        $this->manager(ItemManager::class)->getByName('2008', self::VERSION, self::LANG);
    }
}
