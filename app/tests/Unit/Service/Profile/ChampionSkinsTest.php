<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Profile;

use App\Service\API\ChampionManager;
use App\Service\Profile\ChampionSkins;
use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Storage\ImageTranscoder;
use App\Service\Tools\GoFetcherClient;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * ChampionSkins bridges the stored banner id and the DDragon CDN. Banner URLs
 * are pure functions of "{championId}_{skinNum}" (a cold data layer never blanks
 * the hero); only the human-readable name needs champion detail, and it degrades
 * to the champion id when that read fails.
 */
final class ChampionSkinsTest extends TestCase
{
    private const VERSION = '16.14.1';
    private const LANG = 'en_US';
    private const SPLASH = 'https://ddragon.leagueoflegends.com/cdn/img/champion/splash';
    private const CENTERED = 'https://ddragon.leagueoflegends.com/cdn/img/champion/centered';
    private const LOADING = 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_skins_'.bin2hex(random_bytes(6));
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

    public function testOptionsListsSkinsBaseFirstWithDerivedArt(): void
    {
        $skins = $this->skins($this->fsWithDetail([
            'name'  => 'Ahri',
            'skins' => [
                ['id' => '103000', 'num' => 0, 'name' => 'default'],
                ['id' => '103001', 'num' => 1, 'name' => 'Midnight Ahri'],
                ['id' => '103007', 'num' => 7, 'name' => 'Spirit Blossom Ahri'],
            ],
        ]));

        $options = $skins->options('Ahri', self::VERSION, self::LANG);

        self::assertSame(['Ahri_0', 'Ahri_1', 'Ahri_7'], array_column($options, 'id'));
        // "default" surfaces the champion name, not the raw DDragon token.
        self::assertSame('Ahri', $options[0]['name']);
        self::assertSame('Spirit Blossom Ahri', $options[2]['name']);
        self::assertSame(self::LOADING.'/Ahri_7.jpg', $options[2]['image']);
        self::assertSame(self::CENTERED.'/Ahri_7.jpg', $options[2]['banner']);
    }

    public function testOptionsDropsChromaInlinedSkins(): void
    {
        // Data Dragon inlines each chroma as a standalone skin sharing the
        // CommunityDragon chroma id — here 103002 under parent skin 103001.
        $fs = $this->fsWithDetail([
            'name'  => 'Ahri',
            'key'   => '103',
            'skins' => [
                ['id' => '103000', 'num' => 0, 'name' => 'default'],
                ['id' => '103001', 'num' => 1, 'name' => 'Midnight Ahri'],
                ['id' => '103002', 'num' => 2, 'name' => 'Midnight Ahri (Ruby)'],
            ],
        ]);
        // Pre-seed the chroma cache so getChromas reads storage (no egress).
        $fs->write(
            sprintf('data/%s/cdragon/chromas/103.json', self::VERSION),
            json_encode(['103001' => [['id' => 103002, 'name' => 'x', 'colors' => [], 'image' => 'y']]], JSON_THROW_ON_ERROR),
        );

        $options = $this->skins($fs)->options('Ahri', self::VERSION, self::LANG);

        self::assertSame(['Ahri_0', 'Ahri_1'], array_column($options, 'id'));
    }

    public function testResolveBannerDerivesUrlsAndName(): void
    {
        $skins = $this->skins($this->fsWithDetail([
            'name'  => 'Ahri',
            'skins' => [['id' => '103007', 'num' => 7, 'name' => 'Spirit Blossom Ahri']],
        ]));

        $banner = $skins->resolveBanner('Ahri_7', self::VERSION, self::LANG);

        self::assertNotNull($banner);
        self::assertSame('Ahri', $banner['championId']);
        self::assertSame(7, $banner['num']);
        self::assertSame('Spirit Blossom Ahri', $banner['name']);
        self::assertSame(self::CENTERED.'/Ahri_7.jpg', $banner['banner']);
        self::assertSame(self::SPLASH.'/Ahri_7.jpg', $banner['splash']);
    }

    public function testResolveBannerFallsBackToChampionIdWhenDataLayerDown(): void
    {
        // No detail file on disk → getDetail hits the gateway, which refuses egress;
        // the art still derives, only the label degrades to the champion id.
        $skins = $this->skins(new Filesystem(new LocalFilesystemAdapter($this->dir)), $this->noEgress());

        $banner = $skins->resolveBanner('Ahri_7', self::VERSION, self::LANG);

        self::assertNotNull($banner);
        self::assertSame('Ahri', $banner['name']);
        self::assertSame(self::CENTERED.'/Ahri_7.jpg', $banner['banner']);
    }

    public function testResolveBannerRejectsNull(): void
    {
        $skins = $this->skins(new Filesystem(new LocalFilesystemAdapter($this->dir)), $this->noEgress());

        self::assertNull($skins->resolveBanner(null, self::VERSION, self::LANG));
    }

    /**
     * @return array<string, array{0: string}>
     */
    public static function malformedIds(): array
    {
        return [
            'empty'       => [''],
            'no number'   => ['Ahri'],
            'no champion' => ['_7'],
            'bad char'    => ['Ahri-7'],
            'trailing'    => ['Ahri_'],
        ];
    }

    #[DataProvider('malformedIds')]
    public function testResolveBannerRejectsMalformedIds(string $id): void
    {
        $skins = $this->skins(new Filesystem(new LocalFilesystemAdapter($this->dir)), $this->noEgress());

        self::assertNull($skins->resolveBanner($id, self::VERSION, self::LANG));
    }

    public function testIsWellFormedGatesFormatWithoutEgress(): void
    {
        $skins = $this->skins(new Filesystem(new LocalFilesystemAdapter($this->dir)), $this->noEgress());

        self::assertTrue($skins->isWellFormed('Ahri_7'));
        self::assertTrue($skins->isWellFormed('MonkeyKing_12'));
        self::assertFalse($skins->isWellFormed('Ahri'));
        self::assertFalse($skins->isWellFormed('Ahri_'));
        self::assertFalse($skins->isWellFormed("Ahri_7\ninjected"));
    }

    /** @param array<mixed> $detailNode */
    private function fsWithDetail(array $detailNode, string $champion = 'Ahri'): Filesystem
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $fs->write(
            sprintf('data/%s/%s/championDetail/%s.json', self::VERSION, self::LANG, $champion),
            json_encode(['data' => [$champion => $detailNode]], JSON_THROW_ON_ERROR),
        );

        return $fs;
    }

    private function skins(Filesystem $fs, ?GoFetcherClient $go = null): ChampionSkins
    {
        return new ChampionSkins($this->manager($fs, $go ?? $this->noEgress()));
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

    private function noEgress(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('unexpected DDragon egress');
        }));
    }
}
