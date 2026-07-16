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
 * getData()'s handling of a definitively-absent Data Dragon resource — the fix
 * for the version×language crash surface:
 *  - a 403/404 in en_US (resource predates the version, e.g. runesReforged
 *    before 7.22) resolves to an empty dataset, persisted so the CDN is not
 *    re-hit;
 *  - a 403/404 for a non-default language (locale absent from an old patch)
 *    falls back to en_US;
 *  - a transient upstream failure (5xx) still propagates — never frozen as empty.
 */
final class AbstractManagerDataResolutionTest extends TestCase
{
    private const VERSION = '7.21.1';
    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_datares_'.bin2hex(random_bytes(6));
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

    public function testDefinitiveAbsenceInDefaultLangResolvesToEmptyAndIsPersisted(): void
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $calls = 0;
        $manager = $this->manager($fs, $this->gateway(function () use (&$calls): array {
            $calls++;

            return ['status' => 403];
        }));

        self::assertSame([], $manager->getData(self::VERSION, 'en_US'));
        // Persisted verdict → a fresh manager over the same storage never re-fetches.
        self::assertSame('[]', $fs->read(sprintf('data/%s/en_US/runesReforged.json', self::VERSION)));
        self::assertSame([], $this->manager($fs, $this->noEgress())->getData(self::VERSION, 'en_US'));
        self::assertSame(1, $calls, 'the CDN is hit at most once for an immutable absence');
    }

    public function testMissingLanguageFallsBackToEnUs(): void
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $enBody = [['id' => 8000, 'key' => 'Precision', 'name' => 'Precision', 'icon' => 'x.png']];

        $manager = $this->manager($fs, $this->gateway(static function (string $url) use ($enBody): array {
            if (str_contains($url, '/fr_FR/')) {
                return ['status' => 404]; // locale absent from this old patch
            }

            return ['status' => 200, 'body' => $enBody];
        }));

        // Requested in French, served in English rather than failing.
        self::assertSame($enBody, $manager->getData(self::VERSION, 'fr_FR'));
    }

    public function testTransientUpstreamFailurePropagates(): void
    {
        $fs = new Filesystem(new LocalFilesystemAdapter($this->dir));
        $manager = $this->manager($fs, $this->gateway(static fn (): array => ['status' => 503]));

        $this->expectException(\RuntimeException::class);
        $manager->getData(self::VERSION, 'en_US');
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

    /**
     * Gateway whose per-URL response is decided by $resolve(url): ['status'=>int, 'body'=>?array].
     *
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
            throw new \RuntimeException('unexpected DDragon egress');
        }));
    }
}
