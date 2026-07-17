<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Shared formatting rules of the picker projections.
 *
 * Storage image paths are root-relative without a leading slash ("cdn/blobs/…")
 * while the picker API contract exposes them browser-ready ("/cdn/blobs/…") —
 * that knowledge lives here once. Name ordering is a plain byte-wise compare:
 * good enough across the DDragon locales and stable (PHP sorts are stable
 * since 8.0), so equal names keep their dataset order.
 */
final class PickerFormat
{
    private function __construct() {}

    public static function imagePath(?string $storagePath): ?string
    {
        if ($storagePath === null || $storagePath === '') {
            return null;
        }

        return '/'.ltrim($storagePath, '/');
    }

    /**
     * @param list<array<string, mixed>> $options entries carrying a string 'name'
     * @return list<array<string, mixed>>
     */
    public static function sortByName(array $options): array
    {
        usort($options, static fn (array $a, array $b): int => strcmp((string) $a['name'], (string) $b['name']));

        return $options;
    }
}
