<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Admin;

use App\Service\Admin\ServiceHealthProbe;
use App\Service\Analytics\StorageAnalyticsService;
use Doctrine\DBAL\Connection;
use Doctrine\DBAL\DriverManager;
use League\Flysystem\DirectoryListing;
use League\Flysystem\FilesystemOperator;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Cache\Adapter\ArrayAdapter;
use Symfony\Component\HttpClient\Exception\TransportException;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;
use Symfony\Contracts\HttpClient\HttpClientInterface;

/**
 * Probe semantics: every failure mode degrades to a structured result — a dead
 * service can NEVER throw out of the probe, which is what keeps the monitoring
 * page (and the overview badges) at HTTP 200 whatever the mesh looks like.
 */
final class ServiceHealthProbeTest extends TestCase
{
    private const FETCHER_URL = 'http://go-fetcher:8085/healthz';
    private const API_URL = 'http://go-api:8090/healthz';

    public function testHealthyHttpServiceReportsOkWithLatency(): void
    {
        $probe = $this->probe(http: new MockHttpClient(new MockResponse('ok')));

        $result = $probe->http(self::API_URL);

        self::assertSame(ServiceHealthProbe::STATUS_OK, $result['status']);
        self::assertNull($result['detail']);
        self::assertGreaterThanOrEqual(0, $result['latencyMs']);
    }

    public function testNon200HttpServiceReportsDegraded(): void
    {
        $probe = $this->probe(http: new MockHttpClient(new MockResponse('', ['http_code' => 503])));

        $result = $probe->http(self::API_URL);

        self::assertSame(ServiceHealthProbe::STATUS_DEGRADED, $result['status']);
        self::assertSame('HTTP 503', $result['detail']);
    }

    public function testUnreachableHttpServiceReportsDownWithoutThrowing(): void
    {
        // Closed-port simulation: the transport layer refuses the connection.
        $refused = new MockHttpClient(static function (): never {
            throw new TransportException('Connection refused for "http://go-api:8090/healthz".');
        });

        $result = $this->probe(http: $refused)->http(self::API_URL);

        self::assertSame(ServiceHealthProbe::STATUS_DOWN, $result['status']);
        self::assertStringContainsString('Connection refused', (string) $result['detail']);
    }

    public function testDatabaseLivenessReportsOkEvenWithoutPostgresIntrospection(): void
    {
        // SQLite has neither version() nor pg_database_size: liveness must
        // still pass, with the Postgres-only meta simply absent.
        $result = $this->probe()->postgres();

        self::assertSame(ServiceHealthProbe::STATUS_OK, $result['status']);
        self::assertArrayNotHasKey('databaseBytes', $result['meta']);
    }

    public function testDeadDatabaseReportsDownWithoutThrowing(): void
    {
        $dead = DriverManager::getConnection(['driver' => 'pdo_sqlite', 'path' => '/nonexistent/dir/lodb.sqlite']);

        $result = $this->probe(connection: $dead)->postgres();

        self::assertSame(ServiceHealthProbe::STATUS_DOWN, $result['status']);
        self::assertNotNull($result['detail']);
    }

    public function testMinioOkMirrorsTheStorageReport(): void
    {
        $result = $this->probe()->minio();

        self::assertSame(ServiceHealthProbe::STATUS_OK, $result['status']);
        self::assertSame(0, $result['meta']['objects']);
    }

    public function testMinioFailureReportsDegraded(): void
    {
        $broken = $this->createStub(FilesystemOperator::class);
        $broken->method('listContents')->willThrowException(new \RuntimeException('minio unreachable'));

        $result = $this->probe(storage: new StorageAnalyticsService($broken, new ArrayAdapter()))->minio();

        self::assertSame(ServiceHealthProbe::STATUS_DEGRADED, $result['status']);
        self::assertSame('minio unreachable', $result['detail']);
    }

    public function testAllProbesEveryServiceUnderTheExpectedKeys(): void
    {
        $results = $this->probe(http: new MockHttpClient(new MockResponse('ok')))->all();

        self::assertSame(['postgres', 'minio', 'go-fetcher', 'go-api'], array_keys($results));
        foreach ($results as $result) {
            self::assertContains($result['status'], [
                ServiceHealthProbe::STATUS_OK,
                ServiceHealthProbe::STATUS_DEGRADED,
                ServiceHealthProbe::STATUS_DOWN,
            ]);
        }
    }

    private function probe(
        ?Connection $connection = null,
        ?StorageAnalyticsService $storage = null,
        ?HttpClientInterface $http = null,
    ): ServiceHealthProbe {
        return new ServiceHealthProbe(
            $connection ?? DriverManager::getConnection(['driver' => 'pdo_sqlite', 'memory' => true]),
            $storage ?? $this->emptyStorage(),
            $http ?? new MockHttpClient(new MockResponse('ok')),
            self::FETCHER_URL,
            self::API_URL,
        );
    }

    private function emptyStorage(): StorageAnalyticsService
    {
        $operator = $this->createStub(FilesystemOperator::class);
        $operator->method('listContents')->willReturn(new DirectoryListing([]));

        return new StorageAnalyticsService($operator, new ArrayAdapter());
    }
}
