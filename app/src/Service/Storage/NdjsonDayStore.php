<?php
declare(strict_types=1);

namespace App\Service\Storage;

/**
 * Append-only NDJSON log split into one file per UTC day
 * (`<projectDir>/<directory>/{Y-m-d}.ndjson`).
 *
 * Shared by the analytics event log ({@see \App\Service\Analytics\Storage\EventStore})
 * and the audit journal ({@see \App\Service\Audit\AuditLogStore}): both need the
 * same durability model — a single `file_put_contents(FILE_APPEND | LOCK_EX)`
 * is atomic per line across php-fpm workers, microseconds, no network — and the
 * same day-file bookkeeping. Deliberately NOT the S3 read-merge-write pattern
 * used for manifests: these are high-frequency appends, and a shared mutable
 * object would inherit the documented non-atomic RMW race. Cross-host
 * durability is each domain's rollup job.
 *
 * A subclass owns exactly two things: its typed `append()` signature, and its
 * failure policy — analytics swallows everything (it runs after the response is
 * flushed), audit lets its logger arbitrate.
 */
abstract class NdjsonDayStore
{
    private const EXT = '.ndjson';

    private readonly string $baseDir;

    public function __construct(string $projectDir, string $directory)
    {
        $this->baseDir = rtrim($projectDir, '/\\') . '/' . $directory;
    }

    /**
     * Decode NDJSON lines, skipping blanks and unparseable rows. Public and
     * static because the archived tiers read the very same line format from
     * object storage rather than from a day file.
     *
     * @param iterable<string> $lines
     * @return iterable<array<string, mixed>>
     */
    public static function decodeLines(iterable $lines): iterable
    {
        foreach ($lines as $line) {
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
            yield from self::decodeLines($this->streamLines($handle));
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

    public function hasDay(string $date): bool
    {
        return is_file($this->dayPath($date));
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

    /**
     * Append one row to its day file. Silently drops the row when the directory
     * can't be created or the payload can't be encoded — a log write must never
     * be the reason the action it describes fails.
     *
     * @param array<string, mixed> $row
     */
    protected function appendRow(array $row, string $date): void
    {
        if (!$this->ensureBaseDir()) {
            return;
        }

        $line = json_encode($row, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
        if ($line === false) {
            return;
        }

        file_put_contents($this->dayPath($date), $line . "\n", FILE_APPEND | LOCK_EX);
    }

    private function ensureBaseDir(): bool
    {
        return is_dir($this->baseDir)
            || @mkdir($this->baseDir, 0775, true)
            || is_dir($this->baseDir);
    }

    /**
     * Adapts an open file handle to the line iterable {@see decodeLines} expects.
     *
     * `$handle` is deliberately left untyped: `resource` has no declarable type
     * in PHP, so the phpdoc carries the contract instead. Widening the parameter
     * to `iterable<string>` would only move the problem — something still has to
     * turn the handle into lines — and buys no testability: `decodeLines()` is
     * already the file-free seam, exercised directly by AuditArchiveStore, which
     * feeds it `explode()`ed bytes from object storage.
     *
     * @param resource $handle
     * @return iterable<string>
     */
    private function streamLines($handle): iterable
    {
        while (($line = fgets($handle)) !== false) {
            yield $line;
        }
    }
}
