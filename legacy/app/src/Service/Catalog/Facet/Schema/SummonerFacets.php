<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet\Schema;

use App\Service\API\DatasetRef;
use App\Service\API\Edition\Edition;
use App\Service\API\Edition\SummonerEditionRule;
use App\Service\API\SummonerManager;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Catalog\Facet\FacetKind;
use App\Service\Catalog\Facet\FacetSchemaInterface;
use App\Service\Catalog\GameModeLabels;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Summoner-spell list filters. Modes come from the one curated list
 * ({@see GameModeLabels::facetable()}) — JADE is not among them on purpose:
 * LoL Classic is the edition axis ({@see SummonerEditionRule}), offering it as
 * a mode too would double the same filter.
 */
final class SummonerFacets implements FacetSchemaInterface
{
    private const COOLDOWN_UNIT = 's';

    public function __construct(
        private readonly SummonerManager $summoners,
        private readonly TranslatorInterface $translator,
    ) {}

    public function type(): string
    {
        return 'summoner';
    }

    public function schema(DatasetRef $ref): array
    {
        return [
            new FacetDefinition(
                key: 'mode',
                kind: FacetKind::Choice,
                label: $this->label('mode'),
                group: $this->group('availability'),
                options: $this->modeOptions(),
                isPrimary: true,
            ),
            new FacetDefinition(
                key: 'edition',
                kind: FacetKind::Choice,
                label: $this->label('edition'),
                group: $this->group('availability'),
                options: $this->editionOptions(),
                isPrimary: true,
                isMultiple: false,
            ),
            new FacetDefinition(
                key: 'level',
                kind: FacetKind::Choice,
                label: $this->label('level'),
                group: $this->group('availability'),
                options: $this->levelOptions($ref),
            ),
            new FacetDefinition(
                key: 'cooldown',
                kind: FacetKind::Range,
                label: $this->label('cooldown'),
                group: $this->group('stats'),
                unit: self::COOLDOWN_UNIT,
            ),
        ];
    }

    public function valuesOf(string $id, array $entry, DatasetRef $ref): array
    {
        $facetable = GameModeLabels::facetable();
        $values = [
            'mode'    => array_values(array_filter(
                array_map(strval(...), $entry['modes'] ?? []),
                static fn (string $mode): bool => isset($facetable[$mode]),
            )),
            'edition' => SummonerEditionRule::of($entry)->value,
        ];
        if (isset($entry['summonerLevel'])) {
            $values['level'] = (string) (int) $entry['summonerLevel'];
        }
        if (isset($entry['cooldown'][0]) && is_numeric($entry['cooldown'][0])) {
            $values['cooldown'] = (float) $entry['cooldown'][0];
        }

        return $values;
    }

    /** @return list<array{value: string, label: string}> */
    private function modeOptions(): array
    {
        return array_map(
            static fn (string $mode, string $label): array => ['value' => $mode, 'label' => $label],
            array_keys(GameModeLabels::facetable()),
            GameModeLabels::facetable(),
        );
    }

    /** @return list<array{value: string, label: string}> */
    private function editionOptions(): array
    {
        return array_map(
            fn (Edition $edition): array => [
                'value' => $edition->value,
                'label' => $this->translator->trans('edition.'.$edition->value),
            ],
            Edition::cases(),
        );
    }

    /**
     * The unlock levels present in the patch, ascending.
     *
     * @return list<array{value: string, label: string}>
     */
    private function levelOptions(DatasetRef $ref): array
    {
        $levels = [];
        foreach ($this->summoners->getData($ref->version, $ref->lang)['data'] ?? [] as $entry) {
            if (isset($entry['summonerLevel'])) {
                $levels[(int) $entry['summonerLevel']] = true;
            }
        }
        ksort($levels);

        return array_map(
            static fn (int $level): array => ['value' => (string) $level, 'label' => (string) $level],
            array_keys($levels),
        );
    }

    private function label(string $key): string
    {
        return $this->translator->trans('facet.summoner.'.$key);
    }

    private function group(string $name): string
    {
        return $this->translator->trans('facet.group.'.$name);
    }
}
