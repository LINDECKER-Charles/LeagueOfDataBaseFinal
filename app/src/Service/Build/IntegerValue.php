<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * Lenient int reading shared by the build pipeline (validator, normalizer,
 * projector, view assembler, rune index): Data Dragon ships native ints, but
 * stored structures and JSON round-trips carry int-shaped strings, and every
 * reader was already defensive. Anything else — floats, "12abc", null, bools —
 * is unreadable and yields null, so callers decide the fallback themselves.
 */
final class IntegerValue
{
    private function __construct() {}

    public static function read(mixed $value): ?int
    {
        if (is_int($value)) {
            return $value;
        }
        if (is_string($value) && $value !== '' && (string) (int) $value === $value) {
            return (int) $value;
        }

        return null;
    }
}
