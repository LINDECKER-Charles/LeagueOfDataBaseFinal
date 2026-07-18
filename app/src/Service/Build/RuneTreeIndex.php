<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * Single traversal of a Data Dragon runesReforged tree list, indexed once and
 * queried in the two shapes the build pipeline needs. Centralises the knowledge
 * of the upstream tree shape (`tree.slots[].runes[].id`) that was previously
 * re-walked in {@see BuildStructureValidator} and {@see BuildStructureProjector}.
 *
 * Ids are read leniently ({@see BuildStructureValidator::readInt}: int or
 * int-shaped string) — DDragon ships native ints, but stored structures and
 * JSON round-trips do not, and both callers were already defensive.
 */
final class RuneTreeIndex
{
    /**
     * @param array<int, list<array<int, true>>> $slotsByTree treeId => ordered slots, each a perkId set
     * @param array<int, true>                   $allIds      every style and perk id present in the trees
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
            $treeId = BuildStructureValidator::readInt($tree['id'] ?? null);
            if ($treeId !== null) {
                $allIds[$treeId] = true;
            }

            $slots = [];
            foreach ((array) ($tree['slots'] ?? []) as $slot) {
                $perks = [];
                foreach ((array) ($slot['runes'] ?? []) as $rune) {
                    if (($perkId = BuildStructureValidator::readInt($rune['id'] ?? null)) !== null) {
                        $perks[$perkId] = true;
                        // Perk ids are collected even when their tree carries no id,
                        // preserving BuildStructureProjector::runeIdSet's tolerance.
                        $allIds[$perkId] = true;
                    }
                }
                $slots[] = $perks;
            }

            // Slot structure is keyed only by valid tree ids, preserving
            // BuildStructureValidator::indexTreeSlots (skips id-less trees).
            if ($treeId !== null) {
                $slotsByTree[$treeId] = $slots;
            }
        }

        return new self($slotsByTree, $allIds);
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
