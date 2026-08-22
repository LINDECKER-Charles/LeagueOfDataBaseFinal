<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\DatasetRef;
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
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * A definitive upstream absence (403/404 — the dead icon paths of DDragon's
 * 7.22-8.7 rune back-catalogue) is SETTLED as a null manifest entry: the page
 * renders its placeholder from then on without ever re-asking the CDN.
 * Transient failures are the opposite: never recorded, retried next render.
 */
final class ManagerAbsentImageTest extends TestCase
{
    private const VERSION = '15.1.1';
    private const LANG = 'en_US';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_absent_'.bin2hex(random_bytes(6));
        $fs = $this->storage();
        $fs->write(
            sprintf('data/%s/%s/item.json', self::VERSION, self::LANG),
            json_encode(['type' => 'item', 'data' => [
                '1036' => ['name' => 'Long Sword', 'image' => ['full' => '1036.png']],
                '9999' => ['name' => 'Dead Icon', 'image' => ['full' => '9999.png']],
            ]], JSON_THROW_ON_ERROR),
        );
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

    public function testADefinitiveAbsenceIsSettledOnceAndNeverRefetched(): void
    {
        // Exactly ONE gateway batch is allowed: 1036 delivered, 9999 a clean 404.
        $images = $this->manager($this->oneBatchThen404())
            ->getImages(new DatasetRef(self::VERSION, self::LANG));

        self::assertNotNull($images[1036]);
        self::assertNull($images[9999], 'absent upstream => settled null, not an error');

        // A FRESH manager (new memo + cache) over the same storage must answer
        // from the manifest alone — its gateway refuses any egress.
        $again = $this->manager($this->noEgress())
            ->getImages(new DatasetRef(self::VERSION, self::LANG));

        self::assertSame($images[1036], $again[1036]);
        self::assertNull($again[9999]);
    }

    public function testATransientFailureIsNotSettledAndIsRetried(): void
    {
        // 1036 delivered, 9999 a 503 — must NOT be recorded as absent.
        $first = $this->manager($this->oneBatchThen(503))
            ->getImages(new DatasetRef(self::VERSION, self::LANG));

        self::assertNull($first[9999] ?? null);

        // The next render asks the gateway again for 9999 only, and gets it.
        $recovered = $this->manager($this->recoveringGateway())
            ->getImages(new DatasetRef(self::VERSION, self::LANG));

        self::assertNotNull($recovered[9999], 'a transient failure must be retried');
    }

    private function storage(): Filesystem
    {
        return new Filesystem(new LocalFilesystemAdapter($this->dir));
    }

    private function manager(GoFetcherClient $go): ItemManager
    {
        $fs = $this->storage();

        return new ItemManager(
            $go,
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor(new RequestStack()),
        );
    }

    private function oneBatchThen404(): GoFetcherClient
    {
        return $this->oneBatchThen(404);
    }

    /** One batch: 1036.png delivered, 9999.png answered with $status. */
    private function oneBatchThen(int $status): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient([new MockResponse(json_encode([
            'results' => [
                [
                    'url'         => $this->url('1036'),
                    'status'      => 200,
                    'body_base64' => base64_encode('sword-bytes'),
                ],
                ['url' => $this->url('9999'), 'status' => $status],
            ],
        ], JSON_THROW_ON_ERROR))]));
    }

    /** Serves 9999.png on retry — and would fail the test on any other URL. */
    private function recoveringGateway(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(
            function (string $method, string $u, array $options): MockResponse {
                $urls = json_decode(
                    (string) $options['body'],
                    true,
                    flags: JSON_THROW_ON_ERROR,
                )['urls'];
                self::assertSame([$this->url('9999')], $urls, 'only the unsettled icon retries');

                return new MockResponse(json_encode(['results' => [[
                    'url'         => $this->url('9999'),
                    'status'      => 200,
                    'body_base64' => base64_encode('late-bytes'),
                ]]], JSON_THROW_ON_ERROR));
            },
        ));
    }

    private function noEgress(): GoFetcherClient
    {
        return new GoFetcherClient(new MockHttpClient(static function (): void {
            throw new \RuntimeException('unexpected DDragon egress');
        }));
    }

    private function url(string $id): string
    {
        return sprintf(
            'https://ddragon.leagueoflegends.com/cdn/%s/img/item/%s.png',
            self::VERSION,
            $id,
        );
    }
}
