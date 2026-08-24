<?php
declare(strict_types=1);

namespace App\Tests\Unit\Support;

use Psr\Log\AbstractLogger;

/**
 * In-memory PSR-3 logger for the logging tests.
 *
 * Assertions target the BEHAVIOUR — that a failure path emits a record, at which
 * level, and what its context does (and above all does not) carry — never the
 * rendered text: the formatter belongs to the handler, not to the call site.
 */
final class RecordingLogger extends AbstractLogger
{
    /** @var list<array{level: string, message: string, context: array<string, mixed>}> */
    public array $records = [];

    /**
     * @param mixed $level
     * @param array<string, mixed> $context
     */
    public function log($level, string|\Stringable $message, array $context = []): void
    {
        $this->records[] = [
            'level' => (string) $level,
            'message' => (string) $message,
            'context' => $context,
        ];
    }

    /**
     * Every record emitted under one event key.
     *
     * @return list<array{level: string, message: string, context: array<string, mixed>}>
     */
    public function recordsFor(string $eventKey): array
    {
        return array_values(array_filter(
            $this->records,
            static fn (array $record): bool => $record['message'] === $eventKey,
        ));
    }

    /**
     * The single record emitted under one event key. Fails loudly on 0 or 2+:
     * "it logged something" is not the assertion, "it logged this once" is.
     *
     * @return array{level: string, message: string, context: array<string, mixed>}
     */
    public function only(string $eventKey): array
    {
        $matches = $this->recordsFor($eventKey);
        if (\count($matches) !== 1) {
            throw new \RuntimeException(sprintf(
                'expected exactly one "%s" record, got %d (%s)',
                $eventKey,
                \count($matches),
                implode(', ', array_column($this->records, 'message')) ?: 'nothing logged',
            ));
        }

        return $matches[0];
    }

    /** @return list<string> */
    public function keys(): array
    {
        return array_column($this->records, 'message');
    }
}
