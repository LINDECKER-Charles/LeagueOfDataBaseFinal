<?php
declare(strict_types=1);

namespace App\Service\Analytics\Chart;

/**
 * Human-readable number formatting shared by the admin charts (axis labels,
 * tooltips) and the Twig filters (`|bytes`, `|compact`). Pure and stateless.
 */
final class NumberFormat
{
    private const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    private const BYTE_STEP = 1024;

    public function bytes(int|float $n): string
    {
        $i = 0;
        $n = (float) $n;
        while ($n >= self::BYTE_STEP && $i < count(self::BYTE_UNITS) - 1) {
            $n /= self::BYTE_STEP;
            $i++;
        }

        return ($i === 0 ? (string) (int) $n : number_format($n, 2)) . ' ' . self::BYTE_UNITS[$i];
    }

    public function compact(int|float $n): string
    {
        $n = (float) $n;
        if ($n >= 1_000_000) {
            return $this->stripTrailingZeros(number_format($n / 1_000_000, 1)) . 'M';
        }
        if ($n >= 1_000) {
            return $this->stripTrailingZeros(number_format($n / 1_000, 1)) . 'k';
        }

        return (string) (int) $n;
    }

    /** Thousands-separated integer, used for tooltip and table values. */
    public function integer(int|float $n): string
    {
        return number_format((float) $n, 0, '.', ' ');
    }

    /** "1.0k" reads worse than "1k": drop a decimal that carries no information. */
    private function stripTrailingZeros(string $formatted): string
    {
        return rtrim(rtrim($formatted, '0'), '.');
    }
}
