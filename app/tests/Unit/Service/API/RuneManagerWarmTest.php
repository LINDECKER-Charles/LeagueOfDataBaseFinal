<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\RuneManager;
use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Storage\ImageTranscoder;
use App\Service\Tools\GoFetcherClient;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * The streaming loader's two manager entry points, on the trickiest shape:
 *  - collectPlan() flattens the nested rune tree (tree icon + every keystone/
 *    minor icon) into image => display-name and counts only the missing ones;
 *  - ingest() fetches the missing icons and reports each display name via the
 *    callback (what the SSE endpoint turns into a live "loaded X" event).
 */
final class RuneManagerWarmTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';

    private const TREE_ICON = 'perk-images/Styles/7201_Precision.png';
    private const PTA_ICON = 'perk-images/Styles/Precision/PressTheAttack/PressTheAttack.png';
    private const LT_ICON = 'perk-images/Styles/Precision/LethalTempo/LethalTempoTemp.png';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_rune_'.bin2hex(random_bytes(6));
    }

    protected function tearDown(): void
    {
        if (!is_dir($this->dir)) {
            return;
        }
        $it = new \RecursiveIteratorIterator(
            new \RecursiveDirectoryIterator($this->dir, \FilesystemIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::CHILD_FIRST,
        );
        foreach ($it as $f) {
            $f->isDir() ? rmdir($f->getPathname()) : unlink($f->getPathname());
        }
        rmdir($this->dir);
    }

    public function testCollectPlanFlattensNestedIconsAndCountsMissing(): void
    {
        $fs = $this->seedData();
        // Tree icon already stored; the two runes are not → missing = 2.
        $fs->write(
            sprintf('manifest/%s/runesReforged.json', self::VERSION),
            json_encode([self::TREE_ICON => 'cdn/blobs/tree.png'], JSON_THROW_ON_ERROR),
        );

        $plan = $this->manager($fs, $this->noEgress())->collectPlan(self::VERSION, self::LANG, 0, 1);

        self::assertSame([
            self::TREE_ICON => 'Precision',
            self::PTA_ICON => 'Press the Attack',
            self::LT_ICON => 'Lethal Tempo',
        ], $plan['entries']);
        self::assertSame(2, $plan['missing']);
    }

    public function testIngestStoresMissingIconsAndReportsDisplayNames(): void
    {
        $fs = $this->seedData(); // no manifest → everything missing

        $entries = [
            self::TREE_ICON => 'Precision',
            self::PTA_ICON => 'Press the Attack',
            self::LT_ICON => 'Lethal Tempo',
        ];

        $reported = [];
        $this->manager($fs, $this->gatewayReturningBytes())
            ->ingest(self::VERSION, $entries, static function (string $name) use (&$reported): void {
                $reported[] = $name;
            });

        self::assertSame(['Precision', 'Press the Attack', 'Lethal Tempo'], $reported);
        // The manifest now records all three so a later render is warm.
        $manifest = json_decode(
            $fs->read(sprintf('manifest/%s/runesReforged.json', self::VERSION)),
            true,
            flags: JSON_THROW_ON_ERROR,
        );
        self::assertSame([self::TREE_ICON, self::PTA_ICON, self::LT_ICON], array_keys($manifest));
    }

    public function testConcurrentIngestsMergeInsteadOfOverwritingManifest(): void
    {
        // Two managers over the SAME object storage but SEPARATE caches/memos —
        // i.e. two FPM workers warming the same version concurrently.
        $fs      = $this->seedData();
        $worker1 = $this->manager($fs, $this->gatewayReturningBytes());
        $worker2 = $this->manager($fs, $this->gatewayReturningBytes());

        // Worker 2 reads (and memoises) the still-empty manifest first — exactly
        // what the deferred render path does before kernel.terminate flushes.
        $worker2->collectPlan(self::VERSION, self::LANG, 0, 1);

        // Worker 1 then ingests and commits its entry to storage…
        $worker1->ingest(self::VERSION, [self::PTA_ICON => 'Press the Attack'], static function (): void {});
        // …and only afterwards worker 2 ingests, from its stale empty snapshot.
        $worker2->ingest(self::VERSION, [self::LT_ICON => 'Lethal Tempo'], static function (): void {});

        $manifest = json_decode(
            $fs->read(sprintf('manifest/%s/runesReforged.json', self::VERSION)),
            true,
            flags: JSON_THROW_ON_ERROR,
        );

        // The old blind overwrite dropped worker 1's entry; the merge keeps both.
        self::assertArrayHasKey(self::PTA_ICON, $manifest, "worker 1's entry must survive worker 2's concurrent ingest");
        self::assertArrayHasKey(self::LT_ICON, $manifest);
    }

    private function seedData(): Filesystem
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $fs->write(
            sprintf('data/%s/%s/runesReforged.json', self::VERSION, self::LANG),
            json_encode([[
                'id' => 8000,
                'key' => 'Precision',
                'name' => 'Precision',
                'icon' => self::TREE_ICON,
                'slots' => [[
                    'runes' => [
                        ['id' => 8005, 'key' => 'PressTheAttack', 'name' => 'Press the Attack', 'icon' => self::PTA_ICON],
                        ['id' => 8008, 'key' => 'LethalTempo', 'name' => 'Lethal Tempo', 'icon' => self::LT_ICON],
                    ],
                ]],
            ]], JSON_THROW_ON_ERROR),
        );

        return $fs;
    }

    private function manager(Filesystem $fs, GoFetcherClient $go): RuneManager
    {
        return new RuneManager(
            $go,
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor(new RequestStack()),
        );
    }

    private function noEgress(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('unexpected DDragon egress');
        }));
    }

    /** Echoes back every requested URL with dummy bytes (icon path → base64 body). */
    private function gatewayReturningBytes(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (string $method, string $url, array $options): MockResponse {
            $urls = json_decode((string) $options['body'], true, flags: JSON_THROW_ON_ERROR)['urls'];

            return new MockResponse(
                json_encode(['results' => array_map(
                    static fn (string $u): array => ['url' => $u, 'status' => 200, 'body_base64' => base64_encode('bytes:'.$u)],
                    $urls,
                )], JSON_THROW_ON_ERROR),
                ['response_headers' => ['content-type' => 'application/json']],
            );
        }));
    }
}
