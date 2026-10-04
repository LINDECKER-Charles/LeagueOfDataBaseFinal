<?php
declare(strict_types=1);

namespace LoDbParity;

/**
 * Skins of a detail page, with their chromas.
 *
 * The number is what champion/detail.html.twig uses for the art files: `num`, or the
 * position in the list the page shows when a patch has no `num`. A chroma keeps its
 * colours: its label is derived afterwards by the legacy front's own rule
 * (app/assets/vue/chroma/chromaLabel.ts), run by the parity tool.
 */
final class SkinProjection
{
    private function __construct()
    {
    }

    /**
     * @param list<array<string, mixed>> $skins skins left by withoutChromaSkins()
     * @param array<string, list<array<string, mixed>>> $chromas getChromas(), by skin id
     * @return list<array<string, mixed>>
     */
    public static function of(array $skins, array $chromas): array
    {
        $projected = [];
        foreach (array_values($skins) as $index => $skin) {
            $id = (string) ($skin['id'] ?? '');
            $projected[] = [
                'id'      => $id,
                'number'  => (int) ($skin['num'] ?? $index),
                'name'    => (string) ($skin['name'] ?? ''),
                'chromas' => array_map(self::chroma(...), $chromas[$id] ?? []),
            ];
        }

        return $projected;
    }

    /**
     * @param array<string, mixed> $chroma
     * @return array{id: int, name: string, colors: list<string>}
     */
    private static function chroma(array $chroma): array
    {
        return [
            'id'     => (int) $chroma['id'],
            'name'   => (string) $chroma['name'],
            'colors' => array_values($chroma['colors']),
        ];
    }
}
