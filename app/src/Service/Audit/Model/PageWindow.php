<?php
declare(strict_types=1);

namespace App\Service\Audit\Model;

/**
 * The slice of a result set one page request asks for, and the "one extra row"
 * convention that goes with it: the journal has no index, so a scan proves a
 * next page exists by collecting one row past the window instead of counting
 * the whole thing. Offset, stop condition and cut therefore belong together.
 */
final readonly class PageWindow
{
    public int $offset;

    public function __construct(int $page, public int $perPage)
    {
        $this->offset = (max(1, $page) - 1) * $perPage;
    }

    /** Rows to collect before a scan may stop: the window plus the probe row. */
    public function scanLimit(): int
    {
        return $this->offset + $this->perPage + 1;
    }

    /**
     * @template T
     * @param list<T> $matched rows collected in order, up to {@see scanLimit()}
     * @return array{rows: list<T>, hasMore: bool}
     */
    public function cut(array $matched): array
    {
        return [
            'rows' => array_slice($matched, $this->offset, $this->perPage),
            'hasMore' => count($matched) > $this->offset + $this->perPage,
        ];
    }
}
