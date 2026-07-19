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
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Image-resolution deferral policy: synchronous by default, deferred only for the
 * list/preview render ({@see RuneManager::paginate}). Guards the regression where a
 * detail page (nested rune shape) rendered broken icons on a cold version because
 * it went through the deferred path — now impossible unless a caller opts in.
 */
final class RuneManagerDeferralTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';
    private const TREE_KEY = 'Precision';
    private const MANIFEST = 'manifest/'.self::VERSION.'/runesReforged.json';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_rune_defer_'.bin2hex(random_bytes(6));
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

    /** The rune-detail regression: a cold detail render must resolve icons inline, even under a request. */
    public function testDetailGetImagesResolvesColdIconsInlineWithinRequest(): void
    {
        $fs = $this->seedData();
        [$manager] = $this->manager($fs, $this->gatewayReturningBytes(), withRequest: true);

        // Not wrapped in withDeferral → detail context → must resolve now.
        $images = $manager->getImages(self::VERSION, self::LANG, false, $this->tree());

        self::assertNotNull($images[self::TREE_KEY]['icon'] ?? null, 'cold detail icons must resolve inline');
        self::assertTrue($fs->fileExists(self::MANIFEST), 'inline resolution persists the manifest immediately');
    }

    /** The list render is the one opt-in: cold icons defer to the flush, showing placeholders first. */
    public function testPaginateDefersColdIconsWithinRequest(): void
    {
        $fs = $this->seedData();
        [$manager, $ingestor] = $this->manager($fs, $this->gatewayReturningBytes(), withRequest: true);

        $result = $manager->paginate(self::VERSION, self::LANG, 0, 1);

        self::assertArrayHasKey(self::TREE_KEY, $result['images']);
        self::assertNull($result['images'][self::TREE_KEY]['icon'], 'list icons defer on a cold version (placeholder)');
        self::assertFalse($fs->fileExists(self::MANIFEST), 'nothing ingested during the deferred render');

        $ingestor->flush();
        self::assertTrue($fs->fileExists(self::MANIFEST), 'the queued batch warms the manifest after the response');
    }

    /** CLI/warmup (no request): even the list path ingests inline in a single pass. */
    public function testPaginateResolvesInlineWithoutRequest(): void
    {
        $fs = $this->seedData();
        [$manager] = $this->manager($fs, $this->gatewayReturningBytes(), withRequest: false);

        $result = $manager->paginate(self::VERSION, self::LANG, 0, 1);

        self::assertNotNull($result['images'][self::TREE_KEY]['icon'] ?? null, 'no request → ingest inline');
    }

    /**
     * @return array{0: RuneManager, 1: DeferredImageIngestor}
     */
    private function manager(Filesystem $fs, GoFetcherClient $go, bool $withRequest): array
    {
        $stack = new RequestStack();
        if ($withRequest) {
            $stack->push(new Request());
        }
        $ingestor = new DeferredImageIngestor($stack);
        $manager = new RuneManager($go, $fs, new BlobStore($fs, new ImageTranscoder()), new ArrayAdapter(), $ingestor);

        return [$manager, $ingestor];
    }

    /** @return list<array<string,mixed>> */
    private function tree(): array
    {
        return [[
            'id' => 8000,
            'key' => self::TREE_KEY,
            'name' => 'Precision',
            'icon' => 'perk-images/Styles/7201_Precision.png',
            'slots' => [[
                'runes' => [
                    ['id' => 8005, 'key' => 'PressTheAttack', 'name' => 'Press the Attack', 'icon' => 'perk-images/Styles/Precision/PressTheAttack/PressTheAttack.png'],
                ],
            ]],
        ]];
    }

    private function seedData(): Filesystem
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $fs->write(
            sprintf('data/%s/%s/runesReforged.json', self::VERSION, self::LANG),
            json_encode($this->tree(), JSON_THROW_ON_ERROR),
        );

        return $fs;
    }

    /** Echoes back every requested URL with dummy bytes so ingestion succeeds. */
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
