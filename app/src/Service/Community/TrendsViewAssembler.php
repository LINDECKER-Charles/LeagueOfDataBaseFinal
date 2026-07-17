<?php
declare(strict_types=1);

namespace App\Service\Community;

use App\Entity\Build;
use App\Service\API\ItemManager;
use App\Service\Build\BuildViewAssembler;

/**
 * Projects a page of trending builds onto the visitor's current catalogs:
 * champion portrait + keystone (reusing the "my builds" row projection) and a
 * short excerpt of the first purchased items. Like every build projection,
 * ids missing from the current patch degrade to honest ghost entries and a
 * catalog outage never breaks the page.
 */
final class TrendsViewAssembler
{
    /** Enough tiles to read the build's core at a glance without widening the row. */
    private const ITEMS_EXCERPT_MAX = 6;

    public function __construct(
        private readonly BuildViewAssembler $buildAssembler,
        private readonly ItemManager $itemManager,
    ) {}

    /**
     * @param list<Build> $builds
     * @return list<array<string, mixed>> per row: build, champion, keystone, items
     */
    public function assemble(array $builds, string $version, string $lang): array
    {
        $itemIndex = $this->itemIndex($builds, $version, $lang);

        return array_map(
            fn (Build $build): array => ['build' => $build, 'items' => $this->itemsExcerpt($build, $itemIndex)]
                + $this->buildAssembler->listRow($build, $version, $lang),
            $builds,
        );
    }

    /**
     * One catalog resolution for the whole page: every excerpted id, deduped.
     *
     * @param list<Build> $builds
     * @return array<string, array{id: string, name: string, image: ?string, gold: ?int}>
     */
    private function itemIndex(array $builds, string $version, string $lang): array
    {
        $ids = [];
        foreach ($builds as $build) {
            foreach ($this->excerptIds($build) as $id) {
                $ids[$id] = true;
            }
        }

        try {
            $index = [];
            foreach ($this->itemManager->resolveRelated(array_keys($ids), $version, $lang) as $entry) {
                $index[$entry['id']] = $entry;
            }

            return $index;
        } catch (\Throwable) {
            return []; // transient catalog failure: every tile degrades to a ghost
        }
    }

    /** @return list<string> first items across steps, in purchase order */
    private function excerptIds(Build $build): array
    {
        $ids = [];
        foreach ($build->getSteps() as $step) {
            foreach ((array) ($step['items'] ?? []) as $id) {
                $ids[] = (string) $id;
                if (\count($ids) === self::ITEMS_EXCERPT_MAX) {
                    return $ids;
                }
            }
        }

        return $ids;
    }

    /**
     * @param array<string, array{id: string, name: string, image: ?string, gold: ?int}> $index
     * @return list<array{id: string, name: string, image: ?string, missing: bool}>
     */
    private function itemsExcerpt(Build $build, array $index): array
    {
        return array_map(
            static fn (string $id): array => isset($index[$id])
                ? ['id' => $id, 'name' => $index[$id]['name'], 'image' => $index[$id]['image'], 'missing' => false]
                : ['id' => $id, 'name' => $id, 'image' => null, 'missing' => true],
            $this->excerptIds($build),
        );
    }
}
