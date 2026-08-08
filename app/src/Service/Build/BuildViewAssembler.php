<?php
declare(strict_types=1);

namespace App\Service\Build;

use App\Entity\Build;
use App\Service\API\ChampionManager;
use App\Service\API\DatasetRef;
use App\Service\API\ItemManager;

/**
 * Projects a stored {@see Build} onto the CURRENT (version, lang) catalogs for
 * server-side rendering (share page, "my builds" rows). Rune resolution is
 * delegated to {@see RunePageAssembler}.
 *
 * Build ids are version-scoped: an item/perk/champion written on an older patch
 * may be absent from the current one. Every resolution therefore degrades to an
 * honest ghost entry (`missing: true`, id displayed as name) — this assembler
 * never lets a data gap or a transient catalog failure escape as an exception,
 * so a shared link keeps rendering whatever happens upstream.
 */
final class BuildViewAssembler
{
    public function __construct(
        private readonly ChampionManager $championManager,
        private readonly ItemManager $itemManager,
        private readonly RunePageAssembler $runePages,
    ) {}

    /**
     * Full view-model of the share page, resolved on $version — the build's own
     * pinned patch ({@see renderVersion}). `patchMismatch` is informative only
     * ("written on X, current is Y"): it compares against $currentVersion, the
     * visitor's site-wide selection.
     *
     * @return array<string, mixed>
     */
    public function assemble(
        Build $build,
        string $version,
        string $currentVersion,
        string $lang,
    ): array {
        $steps = $this->stepsVm($build->getSteps(), $version, $lang);

        return [
            'champion' => $this->championVm($build->getChampionId(), $version, $lang),
            'runes' => $this->runePages->page($build->getRunes(), $version, $lang),
            'steps' => $steps,
            'totalGold' => array_sum(array_column($steps, 'gold')),
            'patchMismatch' => $build->getGameVersion() !== ''
                && $build->getGameVersion() !== $currentVersion,
        ];
    }

    /**
     * Version a build renders with: its own pinned patch, else the caller's
     * fallback (legacy rows).
     */
    public static function renderVersion(Build $build, string $fallback): string
    {
        return $build->getGameVersion() !== '' ? $build->getGameVersion() : $fallback;
    }

    /**
     * Light projection for a "my builds" listing row: champion portrait +
     * keystone medallion, both resolved against the current catalogs.
     *
     * @return array{champion: array<string, mixed>, keystone: array<string, mixed>}
     */
    public function listRow(Build $build, string $version, string $lang): array
    {
        return [
            'champion' => $this->championVm($build->getChampionId(), $version, $lang),
            'keystone' => $this->runePages->keystone($build->getRunes(), $version, $lang),
        ];
    }

    /** @return array<string, mixed> */
    private function championVm(string $championId, string $version, string $lang): array
    {
        $dataset = new DatasetRef($version, $lang);

        try {
            $entry = $this->championManager->getData($version, $lang)['data'][$championId] ?? null;
            $image = $entry === null
                ? null
                : ($this->championManager->getImages($dataset, false, [$entry])[0] ?? null);
        } catch (\Throwable) {
            $entry = null;
            $image = null;
        }

        return [
            'id' => $championId,
            'name' => (string) ($entry['name'] ?? $championId),
            'title' => isset($entry['title']) ? (string) $entry['title'] : null,
            'image' => $image,
            'missing' => $entry === null,
        ];
    }

    /**
     * @param list<array<string, mixed>> $steps stored steps shape
     * @return list<array<string, mixed>>
     */
    private function stepsVm(array $steps, string $version, string $lang): array
    {
        $index = $this->itemIndex($steps, $version, $lang);

        return array_map(
            fn (array $step): array => $this->stepVm($step, $index),
            array_values($steps),
        );
    }

    /**
     * One step row: its items resolved through $index — an id absent from the
     * current catalogs degrades to a ghost entry rather than disappearing.
     *
     * @param array<string, mixed>                $step  stored step shape
     * @param array<string, array<string, mixed>> $index resolved items keyed by id
     * @return array<string, mixed>
     */
    private function stepVm(array $step, array $index): array
    {
        $items = array_map(
            static fn (mixed $id): array => $index[(string) $id]
                ?? [
                    'id' => (string) $id,
                    'name' => (string) $id,
                    'image' => null,
                    'gold' => null,
                    'missing' => true,
                ],
            array_values((array) ($step['items'] ?? [])),
        );

        return [
            'label' => (string) ($step['label'] ?? ''),
            'note' => isset($step['note']) && is_string($step['note']) && $step['note'] !== ''
                ? $step['note']
                : null,
            'items' => $items,
            'gold' => array_sum(array_map(
                static fn (array $i): int => (int) ($i['gold'] ?? 0),
                $items,
            )),
        ];
    }

    /**
     * Every item referenced by the steps, resolved in one call and keyed by id.
     *
     * @param list<array<string, mixed>> $steps stored steps shape
     * @return array<string, array<string, mixed>> [] on a transient data-layer failure
     */
    private function itemIndex(array $steps, string $version, string $lang): array
    {
        $ids = [];
        foreach ($steps as $step) {
            foreach ((array) ($step['items'] ?? []) as $id) {
                $ids[(string) $id] = true;
            }
        }

        try {
            $index = [];
            foreach (
                $this->itemManager->resolveRelated(array_keys($ids), $version, $lang) as $entry
            ) {
                $index[$entry['id']] = [...$entry, 'missing' => false];
            }

            return $index;
        } catch (\Throwable) {
            return [];
        }
    }
}
