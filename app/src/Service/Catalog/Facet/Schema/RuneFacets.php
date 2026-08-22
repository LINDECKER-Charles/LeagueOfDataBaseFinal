<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet\Schema;

use App\Service\API\DatasetRef;
use App\Service\API\RuneManager;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Catalog\Facet\FacetKind;
use App\Service\Catalog\Facet\FacetSchemaInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Rune list filters. A rune node carries no attribute of its own worth
 * filtering on: both facets are POSITIONAL — which path it belongs to and
 * which row it sits in — so the template augments each node with its tree key
 * and slot index before asking for values.
 */
final class RuneFacets implements FacetSchemaInterface
{
    public const TREE_KEY = 'tree';
    public const SLOT_KEY = 'slot';
    private const KEYSTONE_SLOT = 0;
    private const SLOT_KEYSTONE = 'keystone';
    private const SLOT_ROW_PREFIX = 'row';
    private const MINOR_ROWS = [1, 2, 3];

    public function __construct(
        private readonly RuneManager $runes,
        private readonly TranslatorInterface $translator,
    ) {}

    public function type(): string
    {
        return 'runesReforged';
    }

    public function schema(DatasetRef $ref): array
    {
        return [
            new FacetDefinition(
                key: 'path',
                kind: FacetKind::Choice,
                label: $this->translator->trans('facet.rune.path'),
                group: $this->translator->trans('facet.group.identity'),
                options: $this->pathOptions($ref),
                isPrimary: true,
            ),
            new FacetDefinition(
                key: 'slot',
                kind: FacetKind::Choice,
                label: $this->translator->trans('facet.rune.slot'),
                group: $this->translator->trans('facet.group.identity'),
                options: $this->slotOptions(),
                isPrimary: true,
            ),
        ];
    }

    public function valuesOf(string $id, array $entry, DatasetRef $ref): array
    {
        $values = [];
        if (isset($entry[self::TREE_KEY])) {
            $values['path'] = (string) $entry[self::TREE_KEY];
        }
        if (isset($entry[self::SLOT_KEY])) {
            $values['slot'] = self::slotToken((int) $entry[self::SLOT_KEY]);
        }

        return $values;
    }

    private static function slotToken(int $slot): string
    {
        return $slot === self::KEYSTONE_SLOT ? self::SLOT_KEYSTONE : self::SLOT_ROW_PREFIX.$slot;
    }

    /**
     * The paths of the patch, in Data Dragon order, labelled by their own name.
     *
     * @return list<array{value: string, label: string}>
     */
    private function pathOptions(DatasetRef $ref): array
    {
        $options = [];
        foreach ($this->runes->getData($ref->version, $ref->lang) as $tree) {
            if (\is_array($tree) && isset($tree['key'])) {
                $options[] = [
                    'value' => (string) $tree['key'],
                    'label' => (string) ($tree['name'] ?? $tree['key']),
                ];
            }
        }

        return $options;
    }

    /** @return list<array{value: string, label: string}> */
    private function slotOptions(): array
    {
        $options = [[
            'value' => self::SLOT_KEYSTONE,
            'label' => $this->translator->trans('facet.rune.slot_keystone'),
        ]];
        foreach (self::MINOR_ROWS as $row) {
            $options[] = [
                'value' => self::slotToken($row),
                'label' => $this->translator->trans('facet.rune.slot_row', ['%n%' => $row]),
            ];
        }

        return $options;
    }
}
