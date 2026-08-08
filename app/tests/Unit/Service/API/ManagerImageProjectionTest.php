<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;

/**
 * getImages() shares one prologue in {@see \App\Service\API\AbstractManager} and
 * varies only by projection. Those four shapes are a public contract — the
 * pickers, the build view and the search endpoints index into them — so they are
 * pinned here: any drift breaks a consumer that cannot be type-checked.
 */
final class ManagerImageProjectionTest extends SeededManagerTestCase
{
    /**
     * POSITIONAL list, and entries without an image node are SKIPPED rather than
     * padded with null — the skip rule ChampionOptionsProjector realigns against.
     */
    public function testChampionImagesAreAPositionalListSkippingImagelessEntries(): void
    {
        $images = $this->manager(ChampionManager::class)
            ->getImages($this->dataset());

        self::assertSame(['cdn/karma.png', 'cdn/kayn.png', 'cdn/wukong.png'], $images);
    }

    public function testItemImagesAreKeyedByDisplayName(): void
    {
        $images = $this->manager(ItemManager::class)->getImages($this->dataset());

        self::assertSame(
            ['Long Sword' => 'cdn/longsword.png', 'Trinity Force' => 'cdn/trinity.png'],
            $images,
        );
    }

    public function testSummonerImagesAreKeyedBySpellId(): void
    {
        $images = $this->manager(SummonerManager::class)->getImages($this->dataset());

        self::assertSame(
            ['SummonerFlash' => 'cdn/flash.png', 'SummonerHeal' => 'cdn/heal.png'],
            $images,
        );
    }

    public function testRuneImagesKeepTheNestedTreeShape(): void
    {
        $images = $this->manager(RuneManager::class)->getImages($this->dataset());

        self::assertSame(
            ['Precision' => [
                'icon'  => 'cdn/precision.png',
                'slots' => [['PressTheAttack' => 'cdn/pta.png']],
            ]],
            $images,
        );
    }

    /** An explicit slice bypasses the dataset read and projects just that slice. */
    public function testAnExplicitSliceIsProjectedOnItsOwn(): void
    {
        $images = $this->manager(ItemManager::class)->getImages(
            $this->dataset(),
            false,
            [['name' => 'Long Sword', 'image' => ['full' => '1036.png']]],
        );

        self::assertSame(['Long Sword' => 'cdn/longsword.png'], $images);
    }

    /** The page slice carries its keys and its meta counters through pagination. */
    public function testPaginateReturnsTheSliceUnderTheTypedKeyWithItsMeta(): void
    {
        $page = $this->manager(ItemManager::class)->paginate($this->dataset(), 1, 2);

        // PHP recasts the numeric map keys to int — the slice keeps them as stored.
        self::assertSame([3078], array_keys($page['items']));
        self::assertSame(
            ['currentPage' => 2, 'nombrePage' => 2, 'itemPerPage' => 1, 'totalItem' => 2],
            array_diff_key($page['meta'], ['type' => null]),
        );
    }
}
