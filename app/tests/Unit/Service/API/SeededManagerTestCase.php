<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\AbstractManager;
use App\Service\API\DatasetRef;
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
 * Harness for the manager contract tests: object storage pre-seeded with the
 * four datasets and their image manifests, and a gateway that fails on any
 * egress — so a test only exercises the projection/lookup logic, never network.
 */
abstract class SeededManagerTestCase extends TestCase
{
    protected const VERSION = '15.1.1';
    protected const LANG = 'en_US';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_mgr_'.bin2hex(random_bytes(6));
        $this->seed(new Filesystem(new LocalFilesystemAdapter($this->dir)));
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

    /** The single seeded dataset every manager contract test reads from. */
    protected function dataset(): DatasetRef
    {
        return new DatasetRef(self::VERSION, self::LANG);
    }

    /**
     * @template T of AbstractManager
     * @param class-string<T> $manager
     * @return T
     */
    protected function manager(string $manager): AbstractManager
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));

        return new $manager(
            new GoFetcherClient(new MockHttpClient(static function (): void {
                throw new \RuntimeException('unexpected DDragon egress');
            })),
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor(new RequestStack()),
        );
    }

    /** @return array<string,mixed> */
    protected function championData(): array
    {
        return ['type' => 'champion', 'data' => [
            'Karma' => ['id' => 'Karma', 'name' => 'Karma', 'image' => ['full' => 'Karma.png']],
            'Kayn'  => ['id' => 'Kayn', 'name' => 'Kayn', 'image' => ['full' => 'Kayn.png']],
            // Real DDragon shape where the id and the display name differ.
            'MonkeyKing' => [
                'id' => 'MonkeyKing',
                'name' => 'Wukong',
                'image' => ['full' => 'MonkeyKing.png'],
            ],
            // No image node: skipped by both imageEntries() and the projection.
            'Ghost' => ['id' => 'Ghost', 'name' => 'Ghost'],
        ]];
    }

    /** @return array<string,mixed> */
    protected function itemData(): array
    {
        return ['type' => 'item', 'data' => [
            '1036' => ['name' => 'Long Sword', 'image' => ['full' => '1036.png']],
            '3078' => ['name' => 'Trinity Force', 'image' => ['full' => '3078.png']],
        ]];
    }

    /** @return array<string,mixed> */
    protected function summonerData(): array
    {
        return ['type' => 'summoner', 'data' => [
            'SummonerFlash' => [
                'id' => 'SummonerFlash',
                'name' => 'Flash',
                'image' => ['full' => 'SummonerFlash.png'],
            ],
            'SummonerHeal' => [
                'id' => 'SummonerHeal',
                'name' => 'Heal',
                'image' => ['full' => 'SummonerHeal.png'],
            ],
        ]];
    }

    /** @return list<array<string,mixed>> */
    protected function runeData(): array
    {
        return [[
            'id' => 8000,
            'key' => 'Precision',
            'name' => 'Precision',
            'icon' => 'perk-images/Styles/7201_Precision.png',
            'slots' => [[
                'runes' => [[
                    'id' => 8005,
                    'key' => 'PressTheAttack',
                    'name' => 'Press the Attack',
                    'icon' => 'perk-images/Styles/Precision/PressTheAttack.png',
                ]],
            ]],
        ]];
    }

    private function seed(Filesystem $fs): void
    {
        $datasets = [
            'champion'      => $this->championData(),
            'item'          => $this->itemData(),
            'summoner'      => $this->summonerData(),
            'runesReforged' => $this->runeData(),
        ];
        foreach ($datasets as $type => $data) {
            $fs->write(
                sprintf('data/%s/%s/%s.json', self::VERSION, self::LANG, $type),
                json_encode($data, JSON_THROW_ON_ERROR),
            );
        }

        foreach ($this->manifests() as $type => $manifest) {
            $fs->write(
                sprintf('manifest/%s/%s.json', self::VERSION, $type),
                json_encode($manifest, JSON_THROW_ON_ERROR),
            );
        }
    }

    /** @return array<string, array<string,string>> image file => stored path, per type */
    protected function manifests(): array
    {
        return [
            'champion' => [
                'Karma.png'      => 'cdn/karma.png',
                'Kayn.png'       => 'cdn/kayn.png',
                'MonkeyKing.png' => 'cdn/wukong.png',
            ],
            'item' => [
                '1036.png' => 'cdn/longsword.png',
                '3078.png' => 'cdn/trinity.png',
            ],
            'summoner' => [
                'SummonerFlash.png' => 'cdn/flash.png',
                'SummonerHeal.png'  => 'cdn/heal.png',
            ],
            'runesReforged' => [
                'perk-images/Styles/7201_Precision.png'            => 'cdn/precision.png',
                'perk-images/Styles/Precision/PressTheAttack.png'  => 'cdn/pta.png',
            ],
        ];
    }
}
