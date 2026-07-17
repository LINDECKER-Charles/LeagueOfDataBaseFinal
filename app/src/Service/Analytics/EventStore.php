<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Append-only local event log, one NDJSON file per UTC day
 * (var/analytics/events/{Y-m-d}.ndjson). Writes are a single
 * `file_put_contents(FILE_APPEND | LOCK_EX)` — atomic per line across php-fpm
 * workers on the same host, microseconds, no network — so the terminate-time
 * recorder adds no perceptible latency.
 *
 * This is deliberately NOT the S3 read-merge-write pattern used for manifests:
 * page views are high frequency, and a shared mutable object would inherit the
 * documented non-atomic RMW race. Durability across hosts / `down -v` is the
 * rollup's job ({@see RollupService}), which folds closed days into immutable
 * MinIO aggregates.
 */
final class EventStore
{
    private const DIR = 'var/analytics/events';
    private const EXT = '.ndjson';

    private readonly string $baseDir;

    public function __construct(
        #[Autowire('%kernel.project_dir%')]
        string $projectDir,
    ) {
        $this->baseDir = rtrim($projectDir, '/\\') . '/' . self::DIR;
    }

    /**
     * Best-effort append. Failures are swallowed: this runs at kernel.terminate,
     * after the response is flushed, and must never turn a served page into an
     * error.
     */
    public function append(RequestEvent $event): void
    {
        try {
            if (!is_dir($this->baseDir) && !@mkdir($this->baseDir, 0775, true) && !is_dir($this->baseDir)) {
                return;
            }

            $line = json_encode($event->toArray(), JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
            if ($line === false) {
                return;
            }

            file_put_contents($this->dayPath($event->at->format('Y-m-d')), $line . "\n", FILE_APPEND | LOCK_EX);
        } catch (\Throwable) {
            // Analytics is best-effort; never propagate into the request lifecycle.
        }
    }

    /**
     * Decoded events for one UTC day, streamed line by line so a large file is
     * never fully held in memory.
     *
     * @return iterable<array<string, mixed>>
     */
    public function readDay(string $date): iterable
    {
        $path = $this->dayPath($date);
        if (!is_file($path)) {
            return;
        }

        $handle = @fopen($path, 'rb');
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

    public function hasDay(string $date): bool
    {
        return is_file($this->dayPath($date));
    }

    public function dayPath(string $date): string
    {
        return $this->baseDir . '/' . $date . self::EXT;
    }

    public function deleteDay(string $date): void
    {
        $path = $this->dayPath($date);
        if (is_file($path)) {
            @unlink($path);
        }
    }
}
