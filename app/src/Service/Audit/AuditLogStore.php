<?php
declare(strict_types=1);

namespace App\Service\Audit;

use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Append-only local audit log, one NDJSON file per UTC day
 * (var/audit/events/{Y-m-d}.ndjson). Same durability model as the analytics
 * {@see \App\Service\Analytics\EventStore}: a single `file_put_contents(FILE_APPEND
 * | LOCK_EX)` is atomic per line across php-fpm workers, microseconds, no network.
 *
 * Cross-host durability and survival across `down -v` is the rollup's job
 * ({@see AuditRollupService}), which archives closed days verbatim into MinIO —
 * verbatim, not aggregated, because an audit trail must preserve every row.
 */
final class AuditLogStore
{
    private const DIR = 'var/audit/events';
    private const EXT = '.ndjson';

    private readonly string $baseDir;

    public function __construct(
        #[Autowire('%kernel.project_dir%')]
        string $projectDir,
    ) {
        $this->baseDir = rtrim($projectDir, '/\\') . '/' . self::DIR;
    }

    /**
     * Best-effort append. Failures are swallowed by {@see AuditLogger}; the store
     * itself surfaces nothing so a logging call can never break its own request.
     */
    public function append(AuditEvent $event): void
    {
        if (!is_dir($this->baseDir) && !@mkdir($this->baseDir, 0775, true) && !is_dir($this->baseDir)) {
            return;
        }

        $line = json_encode($event->toArray(), JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
        if ($line === false) {
            return;
        }

        file_put_contents($this->dayPath($event->at->format('Y-m-d')), $line . "\n", FILE_APPEND | LOCK_EX);
    }

    /**
     * Decoded rows for one UTC day, streamed line by line so a large file is
     * never fully held in memory.
     *
     * @return iterable<array<string, mixed>>
     */
    public function readDay(string $date): iterable
    {
        $handle = @fopen($this->dayPath($date), 'rb');
        if ($handle === false) {
            return;
        }

        try {
            while (($line = fgets($handle)) !== false) {
                $line = trim($line);
                if ($line === '') {
                    continue;
                }
                $row = json_decode($line, true);
                if (is_array($row)) {
                    yield $row;
                }
            }
        } finally {
            fclose($handle);
        }
    }

    /** Raw NDJSON bytes of a day, for verbatim archival. Null when absent/unreadable. */
    public function readRaw(string $date): ?string
    {
        $contents = @file_get_contents($this->dayPath($date));

        return $contents === false ? null : $contents;
    }

    /**
     * Locally present day files (UTC dates), newest first.
     *
     * @return list<string>
     */
    public function days(): array
    {
        $dates = [];
        foreach (glob($this->baseDir . '/*' . self::EXT) ?: [] as $file) {
            $dates[] = basename($file, self::EXT);
        }
        rsort($dates);

        return $dates;
    }

    public function sizeOf(string $date): int
    {
        $size = @filesize($this->dayPath($date));

        return $size === false ? 0 : $size;
    }

    public function deleteDay(string $date): void
    {
        $path = $this->dayPath($date);
        if (is_file($path)) {
            @unlink($path);
        }
    }

    public function dayPath(string $date): string
    {
        return $this->baseDir . '/' . $date . self::EXT;
    }
}
