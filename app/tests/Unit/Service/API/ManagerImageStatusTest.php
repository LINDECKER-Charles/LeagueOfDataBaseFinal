<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Storage\ImageTranscoder;
use App\Service\Tools\GoFetcherClient;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use Psr\Log\NullLogger;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Three states, not two: a name the manifest settles (path, or null for a
 * definitive absence) versus a name still to fetch. The list render exposes
 * the latter as `pending` so the page can draw a refreshable slot instead of
 * the initials it reserves for absences.
 */
final class ManagerImageStatusTest extends SeededManagerTestCase
{
    public function testManifestStatusSeparatesSettledFromPending(): void
    {
        $status = $this->manager(ItemManager::class)
            ->manifestStatus(self::VERSION, ['1036.png', '3078.png', '4242.png', '1036.png']);

        self::assertSame(
            ['1036.png' => 'cdn/longsword.png', '3078.png' => 'cdn/trinity.png'],
            $status['images'],
        );
        self::assertSame(['4242.png'], $status['pending']);
    }

    public function testASettledAbsenceIsNeverPending(): void
    {
        $status = $this->managerWithManifest(['1036.png' => 'cdn/longsword.png', '3078.png' => null])
            ->manifestStatus(self::VERSION, ['3078.png']);

        self::assertSame(['3078.png' => null], $status['images']);
        self::assertSame([], $status['pending']);
    }

    public function testAWarmListRenderHasNothingPending(): void
    {
        $page = $this->manager(ChampionManager::class)->paginate($this->dataset(), 0);

        self::assertSame([], $page['pending']);
    }

    public function testAColdListRenderReportsTheDeferredNamesAsPending(): void
    {
        // Inside a request the list render defers what the manifest lacks — and
        // must say so, keyed by image name for O(1) template lookups.
        $page = $this->managerWithManifest(['1036.png' => 'cdn/longsword.png'])
            ->paginate($this->dataset(), 0);

        self::assertSame(['3078.png' => true], $page['pending']);
        self::assertNull($page['images'][3078]);
        self::assertSame('cdn/longsword.png', $page['images'][1036]);
    }

    public function testEmptyPageCarriesTheFullTemplateContract(): void
    {
        self::assertSame(
            ['items' => [], 'images' => [], 'pending' => [], 'meta' => []],
            $this->manager(ItemManager::class)->emptyPage(),
        );
    }

    /**
     * An item manager over its own storage with the given manifest, inside a
     * main request so cold names defer (and stay pending) instead of fetching.
     *
     * @param array<string,?string> $manifest
     */
    private function managerWithManifest(array $manifest): ItemManager
    {
        $dir = sys_get_temp_dir().'/lodb_status_'.bin2hex(random_bytes(6));
        $fs = new Filesystem(new LocalFilesystemAdapter($dir));
        $fs->write(
            sprintf('data/%s/%s/item.json', self::VERSION, self::LANG),
            json_encode($this->itemData(), JSON_THROW_ON_ERROR),
        );
        $fs->write(
            sprintf('manifest/%s/item.json', self::VERSION),
            json_encode($manifest, JSON_THROW_ON_ERROR),
        );
        $requests = new RequestStack();
        $requests->push(Request::create('/objects'));

        return new ItemManager(
            new GoFetcherClient(new MockHttpClient(static function (): void {
                throw new \RuntimeException('unexpected DDragon egress');
            }), new NullLogger()),
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor($requests, new NullLogger()),
        );
    }
}
