<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\ChampionManager;
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
 * getChromas() — Data Dragon exposes only a boolean `chromas` flag, so the colours
 * and preview art come from CommunityDragon. Covers the slimming (join by skin id,
 * asset-path → URL, colour cleanup, dropping skins with no usable chroma), the
 * object-storage cache (fetched once), and the version→`latest` patch fallback.
 */
final class ChampionManagerChromasTest extends TestCase
{
    private const VERSION = '15.13.1';
    private const KEY = '799';
    private const CDRAGON = 'https://raw.communitydragon.org/%s/plugins/rcp-be-lol-game-data/global/default';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_chromas_'.bin2hex(random_bytes(6));
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

    public function testSlimsCommunityDragonSkinsAndCachesTheResult(): void
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $calls = 0;
        $manager = $this->manager($fs, $this->gateway(function () use (&$calls): array {
            $calls++;

            return ['status' => 200, 'body' => $this->cdragonPayload()];
        }));

        $base = sprintf(self::CDRAGON, '15.13');
        $expected = [
            '799001' => [
                [
                    'id'     => 799002,
                    'name'   => 'Chosen of the Wolf Ambessa (Ruby)',
                    'colors' => ['#D33528', '#D33528'],
                    'image'  => $base.'/v1/champion-chroma-images/799/799002.png',
                ],
                [
                    'id'     => 799003,
                    'name'   => 'Chosen of the Wolf Ambessa (Emerald)',
                    'colors' => ['#0BB874'], // the blank second colour is dropped
                    'image'  => $base.'/v1/champion-chroma-images/799/799003.png',
                ],
            ],
        ];

        self::assertSame($expected, $manager->getChromas(self::KEY, self::VERSION));

        // Persisted → a fresh manager over the same storage never re-fetches.
        self::assertSame(
            $expected,
            $this->manager($fs, $this->noEgress())->getChromas(self::KEY, self::VERSION),
        );
        self::assertSame(1, $calls, 'the chroma source is hit at most once per (version, champion)');
    }

    public function testFallsBackToLatestWhenTheVersionedPatchIsAbsent(): void
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $manager = $this->manager($fs, $this->gateway(function (string $url): array {
            if (str_contains($url, '/15.13/')) {
                return ['status' => 404]; // patch not cut on CommunityDragon yet
            }

            return ['status' => 200, 'body' => $this->cdragonPayload()];
        }));

        $result = $manager->getChromas(self::KEY, self::VERSION);

        self::assertArrayHasKey('799001', $result);
        self::assertStringContainsString(
            sprintf(self::CDRAGON, 'latest').'/v1/champion-chroma-images/799/799002.png',
            $result['799001'][0]['image'],
        );
    }

    public function testNonNumericKeyShortCircuitsWithoutEgress(): void
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $manager = $this->manager($fs, $this->noEgress());

        self::assertSame([], $manager->getChromas('Ambessa', self::VERSION));
    }

    public function testWithoutChromaSkinsDropsDataDragonInlinedChromaEntries(): void
    {
        $manager = $this->manager(new Filesystem(new LocalFilesystemAdapter($this->dir)), $this->noEgress());

        // Data Dragon inlines each chroma as a standalone skin carrying the same id
        // as the CommunityDragon chroma — here 799002/799003 under parent 799001.
        $skins = [
            ['id' => '799000', 'num' => 0, 'name' => 'default'],
            ['id' => '799001', 'num' => 1, 'name' => 'Chosen of the Wolf Ambessa'],
            ['id' => '799002', 'num' => 2, 'name' => 'Chosen of the Wolf Ambessa (Ruby)'],
            ['id' => '799003', 'num' => 3, 'name' => 'Chosen of the Wolf Ambessa (Emerald)'],
        ];
        $chromas = [
            '799001' => [
                ['id' => 799002, 'name' => 'x', 'colors' => [], 'image' => 'a'],
                ['id' => 799003, 'name' => 'y', 'colors' => [], 'image' => 'b'],
            ],
        ];

        self::assertSame(['799000', '799001'], array_column($manager->withoutChromaSkins($skins, $chromas), 'id'));
    }

    public function testWithoutChromaSkinsIsNoOpWhenChromaDataUnavailable(): void
    {
        $manager = $this->manager(new Filesystem(new LocalFilesystemAdapter($this->dir)), $this->noEgress());
        $skins = [
            ['id' => '799000', 'num' => 0, 'name' => 'default'],
            ['id' => '799001', 'num' => 1, 'name' => 'Chosen of the Wolf Ambessa'],
        ];

        self::assertSame($skins, $manager->withoutChromaSkins($skins, []));
    }

    /**
     * CommunityDragon champion node: a base skin (no chromas → skipped), a skin with
     * two chromas, and a skin whose only chroma lacks a path (→ whole skin dropped).
     *
     * @return array<mixed>
     */
    private function cdragonPayload(): array
    {
        return [
            'skins' => [
                ['id' => 799000, 'name' => 'Ambessa', 'chromas' => []],
                ['id' => 799001, 'name' => 'Chosen of the Wolf Ambessa', 'chromas' => [
                    [
                        'id' => 799002,
                        'name' => 'Chosen of the Wolf Ambessa (Ruby)',
                        'chromaPath' => '/lol-game-data/assets/v1/champion-chroma-images/799/799002.png',
                        'colors' => ['#D33528', '#D33528'],
                    ],
                    [
                        'id' => 799003,
                        'name' => 'Chosen of the Wolf Ambessa (Emerald)',
                        'chromaPath' => '/lol-game-data/assets/v1/champion-chroma-images/799/799003.png',
                        'colors' => ['#0BB874', ''],
                    ],
                ]],
                ['id' => 799010, 'name' => 'No Path', 'chromas' => [
                    ['id' => 799011, 'name' => 'No Path (X)', 'chromaPath' => null, 'colors' => []],
                ]],
            ],
        ];
    }

    private function manager(Filesystem $fs, GoFetcherClient $go): ChampionManager
    {
        return new ChampionManager(
            $go,
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor(new RequestStack()),
        );
    }

    /**
     * @param callable(string):array{status:int, body?:array<mixed>} $resolve
     */
    private function gateway(callable $resolve): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (string $method, string $url, array $options) use ($resolve): MockResponse {
            $requested = json_decode((string) $options['body'], true, flags: JSON_THROW_ON_ERROR)['urls'][0];
            $r = $resolve($requested);

            $entry = ['url' => $requested, 'status' => $r['status']];
            if (isset($r['body'])) {
                $entry['body_base64'] = base64_encode(json_encode($r['body'], JSON_THROW_ON_ERROR));
            }

            return new MockResponse(
                json_encode(['results' => [$entry]], JSON_THROW_ON_ERROR),
                ['response_headers' => ['content-type' => 'application/json']],
            );
        }));
    }

    private function noEgress(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('unexpected CommunityDragon egress');
        }));
    }
}
