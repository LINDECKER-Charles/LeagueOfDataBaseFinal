<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\ItemManager;
use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Storage\ImageTranscoder;
use App\Service\Tools\GoFetcherClient;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * resolveRelated() maps recipe IDs (item.into / item.from) to real items.
 * Storage (data + manifest) is pre-seeded so the manager never touches the Go
 * gateway — any egress attempt throws and fails the test (hermeticity).
 */
final class ItemManagerResolveRelatedTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_item_'.bin2hex(random_bytes(6));
    }

    protected function tearDown(): void
    {
        if (!is_dir($this->dir)) {
            return;
        }
        $it = new \RecursiveIteratorIterator(
            new \RecursiveDirectoryIterator($this->dir, \FilesystemIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::CHILD_FIRST
        );
        foreach ($it as $f) {
            $f->isDir() ? rmdir($f->getPathname()) : unlink($f->getPathname());
        }
        rmdir($this->dir);
    }

    public function testResolvesIdsToItemsSkippingUnknownAndDeduping(): void
    {
        $related = $this->makeManager()->resolveRelated(
            ['1058', '9999', '1004', '1004'], // out of order, one unknown, one dup
            self::VERSION,
            self::LANG,
        );

        self::assertSame(
            [
                [
                    'id' => '1058',
                    'name' => 'Needlessly Large Rod',
                    'image' => 'cdn/blobs/bbbb.png',
                    'gold' => 1250,
                ],
                [
                    'id' => '1004',
                    'name' => 'Faerie Charm',
                    'image' => 'cdn/blobs/aaaa.png',
                    'gold' => null,
                ],
            ],
            $related,
            'input order is preserved, unknown IDs dropped, duplicates collapsed;'
                .' total gold surfaced',
        );
    }

    public function testItemWithoutImageYieldsNullPath(): void
    {
        $related = $this->makeManager()->resolveRelated(['3400'], self::VERSION, self::LANG);

        self::assertSame(
            [['id' => '3400', 'name' => 'No Icon Item', 'image' => null, 'gold' => null]],
            $related,
        );
    }

    public function testEmptyInputReturnsEmpty(): void
    {
        self::assertSame([], $this->makeManager()->resolveRelated([], self::VERSION, self::LANG));
    }

    public function testRecipeTreeExpandsComponentsRecursivelyKeepingRepeatsAcrossBranches(): void
    {
        $tree = $this->recipeManager()->recipeTree('3078', self::VERSION, self::LANG);

        self::assertSame('Trinity Force', $tree['name']);
        self::assertSame('cdn/blobs/tf.png', $tree['image']);
        self::assertSame(333, $tree['combine']); // gold.base surfaced as the combine cost
        self::assertSame(
            ['Sheen', 'Phage'],
            array_column($tree['children'], 'name'),
            'from-order preserved',
        );

        [$sheen, $phage] = $tree['children'];
        self::assertSame(
            ['Long Sword', 'Sapphire Crystal'],
            array_column($sheen['children'], 'name'),
        );
        // Long Sword (1036) is a component of BOTH Sheen and Phage — repeats
        // across sibling branches are kept.
        self::assertSame(['Long Sword', 'Ruby Crystal'], array_column($phage['children'], 'name'));
        self::assertSame([], $sheen['children'][0]['children'], 'a base item is a leaf');
    }

    /** A manager over storage holding the flat, unrelated items. */
    private function makeManager(): ItemManager
    {
        $fs = $this->storage();
        $this->seedItems($fs, $this->flatItems());
        $this->seedManifest($fs, [
            '1004.png' => 'cdn/blobs/aaaa.png',
            '1058.png' => 'cdn/blobs/bbbb.png',
        ]);

        return $this->managerOver($fs);
    }

    /** A manager over storage holding the full Trinity Force craft tree. */
    private function recipeManager(): ItemManager
    {
        $fs = $this->storage();
        $this->seedItems($fs, $this->trinityForceCraft());
        $this->seedManifest($fs, [
            '3078.png' => 'cdn/blobs/tf.png', '3057.png' => 'cdn/blobs/sheen.png',
            '3044.png' => 'cdn/blobs/phage.png', '1036.png' => 'cdn/blobs/ls.png',
            '1027.png' => 'cdn/blobs/sap.png', '1028.png' => 'cdn/blobs/rub.png',
        ]);

        return $this->managerOver($fs);
    }

    /** @return array<string, array<string, mixed>> */
    private function flatItems(): array
    {
        return [
            // no gold → null
            '1004' => ['name' => 'Faerie Charm', 'image' => ['full' => '1004.png']],
            '1058' => [
                'name' => 'Needlessly Large Rod',
                'image' => ['full' => '1058.png'],
                'gold' => ['total' => 1250],
            ],
            '3400' => ['name' => 'No Icon Item'], // no image → path stays null, no egress
        ];
    }

    /**
     * The three tiers of the Trinity Force craft tree, assembled into one item
     * map. Union (`+`) rather than array_merge: the IDs are numeric keys, which
     * array_merge would renumber.
     *
     * @return array<string, array<string, mixed>>
     */
    private function trinityForceCraft(): array
    {
        return $this->finishedTrinityForce()
            + $this->trinityForceCombines()
            + $this->trinityForceBaseItems();
    }

    /**
     * Tier 3: the legendary itself, built from two combines.
     *
     * @return array<string, array<string, mixed>>
     */
    private function finishedTrinityForce(): array
    {
        return [
            '3078' => [
                'name' => 'Trinity Force',
                'image' => ['full' => '3078.png'],
                'gold' => ['total' => 3333, 'base' => 333],
                'from' => ['3057', '3044'],
            ],
        ];
    }

    /**
     * Tier 2: both share Long Sword (1036) — the cross-branch repeat under test.
     *
     * @return array<string, array<string, mixed>>
     */
    private function trinityForceCombines(): array
    {
        return [
            '3057' => [
                'name' => 'Sheen',
                'image' => ['full' => '3057.png'],
                'gold' => ['total' => 700, 'base' => 350],
                'from' => ['1036', '1027'],
            ],
            '3044' => [
                'name' => 'Phage',
                'image' => ['full' => '3044.png'],
                'gold' => ['total' => 1100, 'base' => 350],
                'from' => ['1036', '1028'],
            ],
        ];
    }

    /**
     * Tier 1: no `from` — these are the leaves of the tree.
     *
     * @return array<string, array<string, mixed>>
     */
    private function trinityForceBaseItems(): array
    {
        return [
            '1036' => [
                'name' => 'Long Sword',
                'image' => ['full' => '1036.png'],
                'gold' => ['total' => 350],
            ],
            '1027' => [
                'name' => 'Sapphire Crystal',
                'image' => ['full' => '1027.png'],
                'gold' => ['total' => 350],
            ],
            '1028' => [
                'name' => 'Ruby Crystal',
                'image' => ['full' => '1028.png'],
                'gold' => ['total' => 400],
            ],
        ];
    }

    private function storage(): Filesystem
    {
        return new Filesystem(new LocalFilesystemAdapter($this->dir));
    }

    /** @param array<string, array<string, mixed>> $items */
    private function seedItems(Filesystem $fs, array $items): void
    {
        $fs->write(
            sprintf('data/%s/%s/item.json', self::VERSION, self::LANG),
            json_encode(['type' => 'item', 'data' => $items], JSON_THROW_ON_ERROR)
        );
    }

    /** @param array<string, string> $manifest */
    private function seedManifest(Filesystem $fs, array $manifest): void
    {
        $fs->write(
            sprintf('manifest/%s/item.json', self::VERSION),
            json_encode($manifest, JSON_THROW_ON_ERROR)
        );
    }

    private function managerOver(Filesystem $fs): ItemManager
    {
        $noEgress = new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('unexpected DDragon egress');
        }));

        return new ItemManager(
            $noEgress,
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor(new RequestStack()),
        );
    }
}
