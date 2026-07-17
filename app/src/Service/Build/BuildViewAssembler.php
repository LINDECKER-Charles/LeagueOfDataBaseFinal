<?php
declare(strict_types=1);

namespace App\Service\Build;

use App\Entity\Build;
use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;

/**
 * Projects a stored {@see Build} onto the CURRENT (version, lang) catalogs for
 * server-side rendering (share page, "my builds" rows).
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
        private readonly RuneManager $runeManager,
    ) {}

    /**
     * Full view-model of the share page.
     *
     * @return array<string, mixed>
     */
    public function assemble(Build $build, string $version, string $lang): array
    {
        $steps = $this->stepsVm($build->getSteps(), $version, $lang);

        return [
            'champion' => $this->championVm($build->getChampionId(), $version, $lang),
            'runes' => $this->runesVm($build->getRunes(), $version, $lang),
            'steps' => $steps,
            'totalGold' => array_sum(array_column($steps, 'gold')),
            'patchMismatch' => $build->getGameVersion() !== '' && $build->getGameVersion() !== $version,
        ];
    }

    /**
     * Light projection for a "my builds" listing row: champion portrait +
     * keystone medallion, both resolved against the current catalogs.
     *
     * @return array{champion: array<string, mixed>, keystone: array<string, mixed>}
     */
    public function listRow(Build $build, string $version, string $lang): array
    {
        $runes = $this->runesVm($build->getRunes(), $version, $lang);

        return [
            'champion' => $this->championVm($build->getChampionId(), $version, $lang),
            'keystone' => $runes['primary']['keystone'],
        ];
    }

    /** @return array<string, mixed> */
    private function championVm(string $championId, string $version, string $lang): array
    {
        try {
            $entry = $this->championManager->getData($version, $lang)['data'][$championId] ?? null;
            $image = $entry === null
                ? null
                : ($this->championManager->getImages($version, $lang, false, [$entry])[0] ?? null);
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
     * @param array<string, mixed> $runes stored runes shape (see Build docblock)
     * @return array{primary: array<string, mixed>, secondary: array<string, mixed>}
     */
    private function runesVm(array $runes, string $version, string $lang): array
    {
        try {
            $trees = $this->runeManager->getData($version, $lang);
        } catch (\Throwable) {
            $trees = [];
        }

        $primaryTree = $this->treeById($trees, $primaryId = $this->readId($runes['primaryStyleId'] ?? null));
        $secondaryTree = $this->treeById($trees, $secondaryId = $this->readId($runes['secondaryStyleId'] ?? null));
        $images = $this->treeImages(array_values(array_filter([$primaryTree, $secondaryTree])), $version, $lang);

        $primarySel = is_array($runes['primarySelections'] ?? null) ? $runes['primarySelections'] : [];
        $secondarySel = is_array($runes['secondarySelections'] ?? null) ? $runes['secondarySelections'] : [];

        $primary = $this->styleVm($primaryTree, $primaryId, $images);
        $primary['keystone'] = $this->perkVm($primaryTree, $images, $primarySel[0] ?? null);
        $primary['minors'] = array_map(
            fn (int $i): array => $this->perkVm($primaryTree, $images, $primarySel[$i] ?? null),
            [1, 2, 3],
        );

        $secondary = $this->styleVm($secondaryTree, $secondaryId, $images);
        $secondary['perks'] = array_map(
            fn (mixed $id): array => $this->perkVm($secondaryTree, $images, $id),
            array_values($secondarySel),
        );

        return ['primary' => $primary, 'secondary' => $secondary];
    }

    /**
     * @param list<array<string, mixed>> $steps stored steps shape
     * @return list<array<string, mixed>>
     */
    private function stepsVm(array $steps, string $version, string $lang): array
    {
        $ids = [];
        foreach ($steps as $step) {
            foreach ((array) ($step['items'] ?? []) as $id) {
                $ids[(string) $id] = true;
            }
        }

        try {
            $index = [];
            foreach ($this->itemManager->resolveRelated(array_keys($ids), $version, $lang) as $entry) {
                $index[$entry['id']] = [...$entry, 'missing' => false];
            }
        } catch (\Throwable) {
            $index = [];
        }

        $vm = [];
        foreach ($steps as $step) {
            $items = array_map(
                static fn (mixed $id): array => $index[(string) $id]
                    ?? ['id' => (string) $id, 'name' => (string) $id, 'image' => null, 'gold' => null, 'missing' => true],
                array_values((array) ($step['items'] ?? [])),
            );
            $vm[] = [
                'label' => (string) ($step['label'] ?? ''),
                'note' => isset($step['note']) && is_string($step['note']) && $step['note'] !== '' ? $step['note'] : null,
                'items' => $items,
                'gold' => array_sum(array_map(static fn (array $i): int => (int) ($i['gold'] ?? 0), $items)),
            ];
        }

        return $vm;
    }

    /**
     * @param ?array<string, mixed> $tree
     * @param array<string, mixed>  $images RuneManager::getImages map (treeKey => icon + slots)
     * @return array<string, mixed>
     */
    private function styleVm(?array $tree, int $styleId, array $images): array
    {
        if ($tree === null) {
            return ['id' => $styleId, 'key' => null, 'name' => (string) $styleId, 'icon' => null, 'missing' => true];
        }
        $key = (string) ($tree['key'] ?? '');

        return [
            'id' => $styleId,
            'key' => $key,
            'name' => (string) ($tree['name'] ?? $key),
            'icon' => $images[$key]['icon'] ?? null,
            'missing' => false,
        ];
    }

    /**
     * @param ?array<string, mixed> $tree
     * @param array<string, mixed>  $images
     * @return array<string, mixed>
     */
    private function perkVm(?array $tree, array $images, mixed $rawId): array
    {
        $perkId = $this->readId($rawId);
        $found = $tree === null ? null : $this->findPerk($tree, $perkId);
        if ($found === null) {
            return ['id' => $perkId, 'key' => null, 'name' => (string) $perkId, 'icon' => null, 'shortDesc' => null, 'missing' => true];
        }

        $treeKey = (string) ($tree['key'] ?? '');

        return [
            'id' => $perkId,
            'key' => $found['key'],
            'name' => $found['name'],
            'icon' => $images[$treeKey]['slots'][$found['slot']][$found['key']] ?? null,
            'shortDesc' => $found['shortDesc'],
            'missing' => false,
        ];
    }

    /**
     * @param array<string, mixed> $tree
     * @return ?array{slot: int, key: string, name: string, shortDesc: ?string}
     */
    private function findPerk(array $tree, int $perkId): ?array
    {
        foreach ($tree['slots'] ?? [] as $slotIndex => $slot) {
            foreach ($slot['runes'] ?? [] as $rune) {
                if (($rune['id'] ?? null) === $perkId) {
                    return [
                        'slot' => (int) $slotIndex,
                        'key' => (string) ($rune['key'] ?? ''),
                        'name' => (string) ($rune['name'] ?? $rune['key'] ?? $perkId),
                        'shortDesc' => isset($rune['shortDesc']) ? (string) $rune['shortDesc'] : null,
                    ];
                }
            }
        }

        return null;
    }

    /**
     * @param array<mixed> $trees
     * @return ?array<string, mixed>
     */
    private function treeById(array $trees, int $id): ?array
    {
        foreach ($trees as $tree) {
            if (is_array($tree) && ($tree['id'] ?? null) === $id) {
                return $tree;
            }
        }

        return null;
    }

    /**
     * @param list<array<string, mixed>> $trees
     * @return array<string, mixed>
     */
    private function treeImages(array $trees, string $version, string $lang): array
    {
        if ($trees === []) {
            return [];
        }
        try {
            return $this->runeManager->getImages($version, $lang, false, $trees);
        } catch (\Throwable) {
            return [];
        }
    }

    private function readId(mixed $raw): int
    {
        return BuildStructureValidator::readInt($raw) ?? 0;
    }
}
