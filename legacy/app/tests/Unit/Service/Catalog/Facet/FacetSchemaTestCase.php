<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Catalog\Facet;

use App\Service\API\AbstractManager;
use App\Service\API\DatasetRef;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Storage\ImageTranscoder;
use App\Service\Tools\GoFetcherClient;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;
use Psr\Log\NullLogger;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Harness for the facet schemas: datasets seeded per (version, lang) in a
 * throwaway object storage, a manager over it with no egress, and a
 * translator that echoes keys (labels are never the subject here).
 */
abstract class FacetSchemaTestCase extends TestCase
{
    protected const VERSION = '16.16.1';
    protected const LANG = 'en_US';

    private string $dir;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir().'/lodb_facet_'.bin2hex(random_bytes(6));
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

    protected function ref(string $lang = self::LANG): DatasetRef
    {
        return new DatasetRef(self::VERSION, $lang);
    }

    /** @param array<mixed> $payload the raw DDragon file body */
    protected function seed(string $type, array $payload, string $lang = self::LANG): void
    {
        (new Filesystem(new LocalFilesystemAdapter($this->dir)))->write(
            sprintf('data/%s/%s/%s.json', self::VERSION, $lang, $type),
            json_encode($payload, JSON_THROW_ON_ERROR),
        );
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
            }), new NullLogger()),
            $fs,
            new BlobStore($fs, new ImageTranscoder()),
            new ArrayAdapter(),
            new DeferredImageIngestor(new RequestStack(), new NullLogger()),
        );
    }

    protected function translator(): TranslatorInterface
    {
        $translator = $this->createStub(TranslatorInterface::class);
        $translator->method('trans')->willReturnCallback(
            static fn (string $key, array $parameters = []): string
                => $key.($parameters === [] ? '' : json_encode($parameters)),
        );

        return $translator;
    }

    /**
     * @param list<FacetDefinition> $schema
     * @return array<string, FacetDefinition>
     */
    protected static function byKey(array $schema): array
    {
        $indexed = [];
        foreach ($schema as $facet) {
            $indexed[$facet->key] = $facet;
        }

        return $indexed;
    }
}
