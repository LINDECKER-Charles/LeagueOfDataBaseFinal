<?php
declare(strict_types=1);

namespace App\Service\Analytics\Storage;

use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;

/**
 * MinIO gateway for per-day analytics aggregates (analytics/daily/{Y-m-d}.json).
 * These are written once when a day is rolled up ({@see RollupService}) and are
 * immutable thereafter — never the S3 read-merge-write pattern — so the reader
 * can trust them and the local NDJSON for that day can be pruned.
 */
final class DailyAggregateStore
{
    private const PREFIX = 'analytics/daily';
    private const EXT = '.json';

    public function __construct(private readonly FilesystemOperator $ddragonStorage) {}

    /**
     * @return array<string, mixed>|null
     */
    public function read(string $date): ?array
    {
        try {
            $decoded = json_decode($this->ddragonStorage->read($this->key($date)), true);

            return is_array($decoded) ? $decoded : null;
        } catch (\Throwable) {
            return null;
        }
    }

    /**
     * @param array<string, mixed> $aggregate
     */
    public function write(string $date, array $aggregate): void
    {
        $json = json_encode($aggregate, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
        if ($json !== false) {
            $this->ddragonStorage->write($this->key($date), $json);
        }
    }

    public function exists(string $date): bool
    {
        try {
            return $this->ddragonStorage->fileExists($this->key($date));
        } catch (\Throwable) {
            return false;
        }
    }

    /**
     * Rolled-up dates present in MinIO, ascending — the report walks history
     * forward, so this one is deliberately the opposite of the NDJSON tiers
     * (newest first, which is what a "most recent days" scan needs).
     *
     * @return list<string>
     */
    public function dates(): array
    {
        $dates = [];
        try {
            foreach ($this->ddragonStorage->listContents(self::PREFIX, false) as $entry) {
                if ($entry instanceof FileAttributes && str_ends_with($entry->path(), self::EXT)) {
                    $dates[] = pathinfo($entry->path(), PATHINFO_FILENAME);
                }
            }
        } catch (\Throwable) {
            return [];
        }
        sort($dates);

        return $dates;
    }

    private function key(string $date): string
    {
        return self::PREFIX . '/' . $date . self::EXT;
    }
}
