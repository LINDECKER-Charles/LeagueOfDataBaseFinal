<?php
declare(strict_types=1);

namespace App\Service\Audit;

use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;

/**
 * MinIO durability tier for the audit journal (audit/{Y-m-d}.ndjson). Closed
 * local days are archived here verbatim by {@see AuditRollupService} — the raw
 * lines, never an aggregate, because "every action of user X" must survive.
 *
 * The `audit/` prefix lives in the same bucket as the DDragon CDN but is denied
 * at the edge (nginx `location ^~ /cdn/audit/`), exactly like `analytics/`; PHP
 * still reads it internally over the MinIO network.
 */
final class AuditArchiveStore
{
    private const PREFIX = 'audit';
    private const EXT = '.ndjson';

    public function __construct(private readonly FilesystemOperator $ddragonStorage) {}

    public function write(string $date, string $ndjson): void
    {
        $this->ddragonStorage->write($this->key($date), $ndjson);
    }

    /**
     * @return iterable<array<string, mixed>>
     */
    public function readDay(string $date): iterable
    {
        try {
            $contents = $this->ddragonStorage->read($this->key($date));
        } catch (\Throwable) {
            return;
        }

        foreach (explode("\n", $contents) as $line) {
            $line = trim($line);
            if ($line === '') {
                continue;
            }
            $row = json_decode($line, true);
            if (is_array($row)) {
                yield $row;
            }
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
     * Archived dates present in MinIO, newest first.
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
        rsort($dates);

        return $dates;
    }

    public function sizeOf(string $date): int
    {
        try {
            return $this->ddragonStorage->fileSize($this->key($date));
        } catch (\Throwable) {
            return 0;
        }
    }

    public function deleteDay(string $date): void
    {
        try {
            $this->ddragonStorage->delete($this->key($date));
        } catch (\Throwable) {
            // Already gone / transient — retention is idempotent, ignore.
        }
    }

    private function key(string $date): string
    {
        return self::PREFIX . '/' . $date . self::EXT;
    }
}
