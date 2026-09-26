<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Service\API\DatasetRef;
use App\Service\API\RuneManager;

/**
 * Rune paths of runesReforged.json with their slots, as the rune pages read them. A version
 * without the file (before 7.22.1) stores an empty list: no path at all.
 */
final class RuneProjection
{
    private readonly RuneManager $manager;

    public function __construct(
        private readonly LegacyCatalog $catalog,
        private readonly DatasetRef $ref,
    ) {
        $this->manager = $catalog->runes();
    }

    /** @return list<array<string, mixed>> */
    public function entries(): array
    {
        $trees = $this->catalog->requireDataset($this->manager, $this->ref);

        return array_map($this->tree(...), array_values(array_filter($trees, 'is_array')));
    }

    /**
     * @param array<string, mixed> $tree
     * @return array<string, mixed>
     */
    private function tree(array $tree): array
    {
        return [
            'id'    => (int) ($tree['id'] ?? 0),
            'key'   => (string) ($tree['key'] ?? ''),
            'name'  => (string) ($tree['name'] ?? ''),
            'image' => $this->image($tree['icon'] ?? null),
            'slots' => array_map(
                fn (array $slot): array => array_map(
                    $this->rune(...),
                    array_values(array_filter($slot['runes'] ?? [], 'is_array')),
                ),
                array_values(array_filter($tree['slots'] ?? [], 'is_array')),
            ),
        ];
    }

    /**
     * @param array<string, mixed> $rune
     * @return array<string, mixed>
     */
    private function rune(array $rune): array
    {
        return [
            'id'    => (int) ($rune['id'] ?? 0),
            'key'   => (string) ($rune['key'] ?? ''),
            'name'  => (string) ($rune['name'] ?? ''),
            'image' => $this->image($rune['icon'] ?? null),
        ];
    }

    /** @return array{file: string, status: string, url: ?string}|null */
    private function image(mixed $file): ?array
    {
        return ImageStatus::of($this->manager, $this->ref->version, $file);
    }
}
