<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * Single traversal of a Data Dragon runesReforged tree list, indexed once and
 * queried in the two shapes the build pipeline needs. Centralises the knowledge
 * of the upstream tree shape (`tree.slots[].runes[].id`) that was previously
 * re-walked in {@see BuildStructureValidator} and {@see BuildStructureProjector}.
 *
 * Ids are read leniently ({@see IntegerValue::read}: int or int-shaped string) —
 * DDragon ships native ints, but stored structures and JSON round-trips do not,
 * and both callers were already defensive.
 */
final class RuneTreeIndex
{
    /**
     * @param array<int, list<array<int, true>>> $slotsByTree treeId => ordered slots,
     *                                                        each a perkId set
     * @param array<int, true>                   $allIds      every style and perk id
     *                                                        present in the trees
     */
    private function __construct(
        private readonly array $slotsByTree,
        private readonly array $allIds,
    ) {}

    /**
     * @param array<mixed> $runeTrees raw runesReforged top-level list
     */
    public static function fromTrees(array $runeTrees): self
    {
        $slotsByTree = [];
        $allIds = [];

        foreach ($runeTrees as $tree) {
            $treeId = IntegerValue::read($tree['id'] ?? null);
            if ($treeId !== null) {
                $allIds[$treeId] = true;
            }

            $slots = self::slotsOf($tree);
            foreach ($slots as $perks) {
                // Perk ids count even when their tree carries no id: a stored
                // structure may still reference a perk of an id-less tree.
                $allIds += $perks;
            }

            // Slot lookup is keyed by tree id only: an id-less tree is unaddressable,
            // so its slots would never be reachable anyway.
            if ($treeId !== null) {
                $slotsByTree[$treeId] = $slots;
            }
        }

        return new self($slotsByTree, $allIds);
    }

    /**
     * Ordered perk sets of one tree — one entry per slot, in DDragon's own order
     * (slot 0 holds the keystones).
     *
     * @param mixed $tree one runesReforged tree node
     * @return list<array<int, true>>
     */
    private static function slotsOf(mixed $tree): array
    {
        $slots = [];
        foreach ((array) ($tree['slots'] ?? []) as $slot) {
            $perks = [];
            foreach ((array) ($slot['runes'] ?? []) as $rune) {
                $perkId = IntegerValue::read($rune['id'] ?? null);
                if ($perkId !== null) {
                    $perks[$perkId] = true;
                }
            }
            $slots[] = $perks;
        }

        return $slots;
    }

    /**
     * treeId => 4 slot maps (perkId => true). Slot order is DDragon's own; the
     * keystone slot is index 0 by upstream contract.
     *
     * @return array<int, list<array<int, true>>>
     */
    public function slotsByTree(): array
    {
        return $this->slotsByTree;
    }

    /**
     * Every style and perk id present in the trees.
     *
     * @return array<int, true>
     */
    public function allIds(): array
    {
        return $this->allIds;
    }
}
