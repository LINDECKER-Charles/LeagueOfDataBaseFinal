<?php
declare(strict_types=1);

namespace App\Service\Admin;

use App\Service\Analytics\StorageAnalyticsService;
use Doctrine\DBAL\Connection;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Contracts\HttpClient\HttpClientInterface;

/**
 * Health probes of the internal service mesh for the admin monitoring page:
 * Postgres (liveness + version + database size), MinIO (through the memoised
 * storage report), go-fetcher and go-api (/healthz). Endpoints are Docker-
 * internal ONLY — the egress invariant (all external fetches go through the
 * Go gateway) stays intact. A probe never throws: any failure degrades to a
 * `down` result carrying the reason.
 */
final class ServiceHealthProbe
{
    public const STATUS_OK = 'ok';
    public const STATUS_DEGRADED = 'degraded';
    public const STATUS_DOWN = 'down';

    private const HTTP_TIMEOUT_S = 2.0;
    private const REASON_MAX_LENGTH = 140;

    public function __construct(
        private readonly Connection $connection,
        private readonly StorageAnalyticsService $storage,
        private readonly HttpClientInterface $httpClient,
        #[Autowire(param: 'admin.go_fetcher_health_url')] private readonly string $goFetcherHealthUrl,
        #[Autowire(param: 'admin.go_api_health_url')] private readonly string $goApiHealthUrl,
    ) {}

    /** @return array<string, array{status: string, latencyMs: int, detail: ?string, meta: array<string, mixed>}> */
    public function all(): array
    {
        return [
            'postgres' => $this->postgres(),
            'minio' => $this->minio(),
            'go-fetcher' => $this->http($this->goFetcherHealthUrl),
            'go-api' => $this->http($this->goApiHealthUrl),
        ];
    }

    /** @return array{status: string, latencyMs: int, detail: ?string, meta: array<string, mixed>} */
    public function postgres(): array
    {
        $start = microtime(true);
        try {
            $this->connection->executeQuery('SELECT 1')->fetchOne();
        } catch (\Throwable $e) {
            return $this->result(self::STATUS_DOWN, $start, $this->reason($e));
        }

        return $this->result(self::STATUS_OK, $start, null, $this->postgresMeta());
    }

    /** @return array{status: string, latencyMs: int, detail: ?string, meta: array<string, mixed>} */
    public function minio(): array
    {
        $start = microtime(true);
        $report = $this->storage->report();

        if (($report['ok'] ?? false) !== true) {
            return $this->result(self::STATUS_DEGRADED, $start, (string) ($report['error'] ?? 'rapport indisponible'));
        }

        return $this->result(self::STATUS_OK, $start, null, [
            'objects' => (int) ($report['total']['objects'] ?? 0),
            'bytes' => (int) ($report['total']['bytes'] ?? 0),
        ]);
    }

    /** @return array{status: string, latencyMs: int, detail: ?string, meta: array<string, mixed>} */
    public function http(string $url): array
    {
        $start = microtime(true);
        try {
            $status = $this->httpClient
                ->request('GET', $url, ['timeout' => self::HTTP_TIMEOUT_S, 'max_duration' => self::HTTP_TIMEOUT_S])
                ->getStatusCode();
        } catch (\Throwable $e) {
            return $this->result(self::STATUS_DOWN, $start, $this->reason($e));
        }

        return $status === 200
            ? $this->result(self::STATUS_OK, $start, null)
            : $this->result(self::STATUS_DEGRADED, $start, sprintf('HTTP %d', $status));
    }

    /** @return array<string, mixed> */
    private function postgresMeta(): array
    {
        // Best-effort: version/size are Postgres-specific; their absence (e.g.
        // the SQLite backing unit tests) must not fail a proven-alive probe.
        $meta = [];
        try {
            $version = (string) $this->connection->fetchOne('SELECT version()');
            $meta['version'] = implode(' ', array_slice(explode(' ', $version), 0, 2));
        } catch (\Throwable) {
            // Not Postgres (or restricted) — liveness already established.
        }
        try {
            $meta['databaseBytes'] = (int) $this->connection->fetchOne('SELECT pg_database_size(current_database())');
        } catch (\Throwable) {
            // Same rationale as above.
        }

        return $meta;
    }

    /**
     * @param array<string, mixed> $meta
     * @return array{status: string, latencyMs: int, detail: ?string, meta: array<string, mixed>}
     */
    private function result(string $status, float $start, ?string $detail, array $meta = []): array
    {
        return [
            'status' => $status,
            'latencyMs' => (int) round((microtime(true) - $start) * 1000),
            'detail' => $detail,
            'meta' => $meta,
        ];
    }

    private function reason(\Throwable $e): string
    {
        $message = $e->getMessage() !== '' ? $e->getMessage() : $e::class;

        return mb_substr($message, 0, self::REASON_MAX_LENGTH);
    }
}
