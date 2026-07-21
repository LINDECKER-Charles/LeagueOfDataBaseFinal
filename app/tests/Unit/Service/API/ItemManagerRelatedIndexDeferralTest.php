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
use Symfony\Component\HttpClient\Response\MockResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * relatedIndex() is a LIST-render helper (the /objects evolution chips). Its cold
 * icon batch must defer to kernel.terminate like paginate()'s primary icons —
 * otherwise a non-warm patch blocks the whole list response on the union of every
 * item's evolution icons (the "switch version then navigate = long lag" bug).
 *
 * Guards both directions: deferred under a request (placeholder now, warm after
 * the response), inline without one (CLI/warmup still resolves in a single pass).
 */
final class ItemManagerRelatedIndexDeferralTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';
    private const MANIFEST = 'manifest/'.self::VERSION.'/item.json';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_item_related_'.bin2hex(random_bytes(6));
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

    /** Under a request (list render): the cold evolution batch defers — nothing ingested inline. */
    public function testRelatedIndexDefersColdIconsWithinRequest(): void
    {
        $fs = $this->seedData();
        [$manager, $ingestor] = $this->manager($fs, withRequest: true);

        $index = $manager->relatedIndex($this->items(), self::VERSION, self::LANG);

        self::assertArrayHasKey('3078', $index, 'the evolution target is indexed by id regardless of icon warmth');
        self::assertNull($index['3078']['image'], 'a cold evolution icon defers on a list render (placeholder)');
        self::assertFalse($fs->fileExists(self::MANIFEST), 'nothing ingested during the deferred render');

        $ingestor->flush();
        self::assertTrue($fs->fileExists(self::MANIFEST), 'the queued batch warms the manifest after the response');
    }

    /** No request (CLI/warmup): even the list helper resolves inline in a single pass. */
    public function testRelatedIndexResolvesInlineWithoutRequest(): void
    {
        $fs = $this->seedData();
        [$manager] = $this->manager($fs, withRequest: false);

        $index = $manager->relatedIndex($this->items(), self::VERSION, self::LANG);

        self::assertNotNull($index['3078']['image'] ?? null, 'no request → ingest inline');
        self::assertTrue($fs->fileExists(self::MANIFEST), 'inline resolution persists the manifest immediately');
    }

    /**
     * @return array{0: ItemManager, 1: DeferredImageIngestor}
     */
    private function manager(Filesystem $fs, bool $withRequest): array
    {
        $stack = new RequestStack();
        if ($withRequest) {
            $stack->push(new Request());
        }
        $ingestor = new DeferredImageIngestor($stack);
        $manager = new ItemManager($this->gatewayReturningBytes(), $fs, new BlobStore($fs, new ImageTranscoder()), new ArrayAdapter(), $ingestor);

        return [$manager, $ingestor];
    }

    /** Two items where the base builds INTO the finished one — relatedIndex resolves that target's icon. */
    private function items(): array
    {
        return [
            ['name' => 'Long Sword', 'image' => ['full' => '1036.png'], 'into' => ['3078']],
            ['name' => 'Trinity Force', 'image' => ['full' => '3078.png'], 'gold' => ['total' => 3333]],
        ];
    }

    private function seedData(): Filesystem
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        // Data only — NO manifest, so every evolution icon is a cold miss.
        $fs->write(
            sprintf('data/%s/%s/item.json', self::VERSION, self::LANG),
            json_encode(['type' => 'item', 'data' => [
                '1036' => ['name' => 'Long Sword', 'image' => ['full' => '1036.png'], 'into' => ['3078']],
                '3078' => ['name' => 'Trinity Force', 'image' => ['full' => '3078.png'], 'gold' => ['total' => 3333]],
            ]], JSON_THROW_ON_ERROR),
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
