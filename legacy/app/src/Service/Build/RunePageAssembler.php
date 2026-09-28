<?php
declare(strict_types=1);

namespace App\Service\Build;

use App\Service\API\DatasetRef;
use App\Service\API\RuneManager;

/**
 * Resolves a stored rune page against a (version, lang) rune catalog, for
 * server-side rendering. Split out of {@see BuildViewAssembler}: rune trees are
 * the only part of a build with their own nested shape (styles, slots, perks)
 * and their own image map, so they change for their own reasons.
 *
 * Same degradation contract as the assembler it serves: perk/style ids are
 * version-scoped, so anything absent from the target catalog — or a transient
 * data-layer failure — yields a ghost entry (`missing: true`, id as name)
 * instead of an exception.
 */
final class RunePageAssembler
{
    public function __construct(private readonly RuneManager $runeManager) {}

    /**
     * Full rune page of the share view: both styles with their icons, the
     * keystone, the three primary minors and the two secondary picks.
     *
     * @param array<string, mixed> $runes stored runes shape (see Build docblock)
     * @return array{
     *     primary: array<string, mixed>,   style view + keystone + minors
     *     secondary: array<string, mixed>, style view + perks
     * }
     */
    public function page(array $runes, string $version, string $lang): array
    {
        $trees = $this->trees($version, $lang);
        $primaryId = $this->readId($runes['primaryStyleId'] ?? null);
        $secondaryId = $this->readId($runes['secondaryStyleId'] ?? null);
        $primaryTree = $this->treeById($trees, $primaryId);
        $secondaryTree = $this->treeById($trees, $secondaryId);
        $images = $this->treeImages([$primaryTree, $secondaryTree], $version, $lang);

        return [
            'primary' => [
                ...$this->styleView($primaryTree, $primaryId, $images),
                ...$this->primaryPicks($primaryTree, $images, $this->selections($runes, 'primary')),
            ],
            'secondary' => [
                ...$this->styleView($secondaryTree, $secondaryId, $images),
                'perks' => array_map(
                    fn (mixed $id): array => $this->perkView($secondaryTree, $images, $id),
                    array_values($this->selections($runes, 'secondary')),
                ),
            ],
        ];
    }

    /**
     * Keystone alone, for a listing row: the secondary tree, its images and the
     * five other perk lookups the share page needs are never touched.
     *
     * @param array<string, mixed> $runes stored runes shape
     * @return array<string, mixed> a perk view
     */
    public function keystone(array $runes, string $version, string $lang): array
    {
        $tree = $this->treeById(
            $this->trees($version, $lang),
            $this->readId($runes['primaryStyleId'] ?? null),
        );
        $picks = $this->selections($runes, 'primary');

        return $this->perkView(
            $tree,
            $this->treeImages([$tree], $version, $lang),
            $picks[BuildStructureValidator::KEYSTONE_SLOT] ?? null,
        );
    }

    /**
     * Primary picks, slot-ordered: the keystone then the minor slots — the split
     * the rune contract itself names.
     *
     * @param ?array<string, mixed> $tree
     * @param array<string, mixed>  $images
     * @param array<mixed>          $picks  stored primarySelections
     * @return array{keystone: array<string, mixed>, minors: list<array<string, mixed>>}
     */
    private function primaryPicks(?array $tree, array $images, array $picks): array
    {
        $minorSlots = range(
            BuildStructureValidator::FIRST_MINOR_SLOT,
            BuildStructureValidator::PRIMARY_PICKS - 1,
        );

        return [
            'keystone' => $this->perkView(
                $tree,
                $images,
                $picks[BuildStructureValidator::KEYSTONE_SLOT] ?? null,
            ),
            'minors' => array_map(
                fn (int $slot): array => $this->perkView($tree, $images, $picks[$slot] ?? null),
                $minorSlots,
            ),
        ];
    }

    /**
     * @param array<string, mixed>  $runes stored runes shape
     * @param 'primary'|'secondary' $path
     * @return array<mixed> the stored selections, [] when unreadable
     */
    private function selections(array $runes, string $path): array
    {
        $selections = $runes[$path.'Selections'] ?? null;

        return is_array($selections) ? $selections : [];
    }

    /** @return array<mixed> raw rune trees, [] on a transient data-layer failure */
    private function trees(string $version, string $lang): array
    {
        try {
            return $this->runeManager->getData($version, $lang);
        } catch (\Throwable) {
            return [];
        }
    }

    /**
     * @param ?array<string, mixed> $tree
     * @param array<string, mixed>  $images RuneManager::getImages map (treeKey => icon + slots)
     * @return array<string, mixed>
     */
    private function styleView(?array $tree, int $styleId, array $images): array
    {
        if ($tree === null) {
            return [
                'id' => $styleId,
                'key' => null,
                'name' => (string) $styleId,
                'icon' => null,
                'missing' => true,
            ];
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
    private function perkView(?array $tree, array $images, mixed $rawId): array
    {
        $perkId = $this->readId($rawId);
        $found = $tree === null ? null : $this->findPerk($tree, $perkId);
        if ($found === null) {
            return [
                'id' => $perkId,
                'key' => null,
                'name' => (string) $perkId,
                'icon' => null,
                'shortDesc' => null,
                'missing' => true,
            ];
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
                    return $this->perkDescriptor($rune, (int) $slotIndex, $perkId);
                }
            }
        }

        return null;
    }

    /**
     * The parts of a DDragon perk node the views need, with the slot it sits in
     * (the icon map is keyed by slot then perk key).
     *
     * @param array<string, mixed> $rune
     * @return array{slot: int, key: string, name: string, shortDesc: ?string}
     */
    private function perkDescriptor(array $rune, int $slot, int $perkId): array
    {
        return [
            'slot' => $slot,
            'key' => (string) ($rune['key'] ?? ''),
            'name' => (string) ($rune['name'] ?? $rune['key'] ?? $perkId),
            'shortDesc' => isset($rune['shortDesc']) ? (string) $rune['shortDesc'] : null,
        ];
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
     * @param list<?array<string, mixed>> $trees unresolved styles are skipped
     * @return array<string, mixed>
     */
    private function treeImages(array $trees, string $version, string $lang): array
    {
        $known = array_values(array_filter($trees));
        if ($known === []) {
            return [];
        }
        try {
            return $this->runeManager->getImages(new DatasetRef($version, $lang), false, $known);
        } catch (\Throwable) {
            return [];
        }
    }

    private function readId(mixed $raw): int
    {
        return IntegerValue::read($raw) ?? BuildStructureNormalizer::UNSET_PERK_ID;
    }
}
