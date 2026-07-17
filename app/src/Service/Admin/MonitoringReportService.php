<?php
declare(strict_types=1);

namespace App\Service\Admin;

use App\Repository\ApiKeyRepository;
use App\Repository\BuildRepository;
use App\Repository\BuildVoteRepository;
use App\Repository\DonationRepository;
use App\Repository\UserRepository;
use Doctrine\DBAL\ArrayParameterType;
use Doctrine\DBAL\Connection;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

/**
 * Aggregated monitoring report: service health (probes), application counters
 * and storage volumes. Memoised 30 s in ddragon.cache (same pattern as the
 * storage report, `?refresh=1` busts it) so the page and the overview badges
 * share one probe round. Every section degrades gracefully — a dead Postgres
 * yields `ok=false` counters next to live probe results, never a 500.
 */
final class MonitoringReportService
{
    private const CACHE_KEY = 'admin.monitoring.report';
    private const CACHE_TTL = 30;
    private const NEW_USERS_WINDOW = '-7 days';
    /** User-data tables whose on-disk weight the panel tracks. */
    private const TRACKED_TABLES = ['users', 'builds', 'build_votes', 'donations', 'api_keys', 'api_usage'];

    public function __construct(
        private readonly ServiceHealthProbe $probe,
        private readonly UserRepository $users,
        private readonly BuildRepository $builds,
        private readonly BuildVoteRepository $votes,
        private readonly DonationRepository $donations,
        private readonly ApiKeyRepository $apiKeys,
        private readonly Connection $connection,
        #[Autowire(service: 'ddragon.cache')] private readonly CacheInterface $cache,
    ) {}

    /** @return array<string, mixed> */
    public function report(bool $fresh = false): array
    {
        if ($fresh) {
            $this->cache->delete(self::CACHE_KEY);
        }

        return $this->cache->get(self::CACHE_KEY, function (ItemInterface $item): array {
            $item->expiresAfter(self::CACHE_TTL);

            return [
                'generatedAt' => new \DateTimeImmutable()->format('Y-m-d H:i:s T'),
                'services' => $this->probe->all(),
                'counters' => $this->counters(),
                'volumes' => $this->volumes(),
            ];
        });
    }

    /** @return array<string, mixed> */
    private function counters(): array
    {
        try {
            $today = new \DateTimeImmutable('today');

            return [
                'ok' => true,
                'users' => [
                    'total' => $this->users->countAll(),
                    'newWeek' => $this->users->countNewSince(new \DateTimeImmutable(self::NEW_USERS_WINDOW)),
                    'banned' => $this->users->countBanned(),
                ],
                'builds' => ['total' => $this->builds->countAll(), 'public' => $this->builds->countPublic()],
                'votes' => $this->votes->count([]),
                'donations' => ['count' => $this->donations->countAll(), 'totalCents' => $this->donations->sumAll()],
                'apiKeysActive' => $this->apiKeys->countActive(),
                'apiRequests' => [
                    'today' => $this->apiKeys->sumRequestsSince($today),
                    'month' => $this->apiKeys->sumRequestsSince($today->modify('first day of this month')),
                ],
            ];
        } catch (\Throwable $e) {
            return ['ok' => false, 'error' => $e->getMessage()];
        }
    }

    /** @return array{messengerPending: int, tables: list<array{name: string, bytes: int}>} */
    private function volumes(): array
    {
        return [
            'messengerPending' => $this->messengerPending(),
            'tables' => $this->tableSizes(),
        ];
    }

    private function messengerPending(): int
    {
        try {
            return (int) $this->connection->fetchOne('SELECT COUNT(*) FROM messenger_messages');
        } catch (\Throwable) {
            // Doctrine transport auto-creates the table on first dispatch —
            // absent table simply means nothing was ever queued.
            return 0;
        }
    }

    /** @return list<array{name: string, bytes: int}> */
    private function tableSizes(): array
    {
        try {
            $rows = $this->connection->executeQuery(
                'SELECT c.relname AS name, pg_total_relation_size(c.oid) AS bytes
                   FROM pg_class c
                   JOIN pg_namespace n ON n.oid = c.relnamespace
                  WHERE n.nspname = current_schema() AND c.relkind = \'r\' AND c.relname IN (?)
                  ORDER BY bytes DESC',
                [self::TRACKED_TABLES],
                [ArrayParameterType::STRING],
            )->fetchAllAssociative();
        } catch (\Throwable) {
            // Postgres-only introspection — empty list on any other backend/outage.
            return [];
        }

        return array_map(static fn (array $row): array => [
            'name' => (string) $row['name'],
            'bytes' => (int) $row['bytes'],
        ], $rows);
    }
}
