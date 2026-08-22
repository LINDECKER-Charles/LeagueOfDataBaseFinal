<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet\Schema;

use App\Service\API\ChampionManager;
use App\Service\API\DatasetRef;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Catalog\Facet\FacetKind;
use App\Service\Catalog\Facet\FacetSchemaInterface;
use App\Stat\GameStat;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Champion list filters. Two normalisations live here and nowhere else:
 *  - the resource (`partype`) is a LOCALISED string in Data Dragon ("Energy" /
 *    "Énergie" / "에너지"), so the facet token is derived from the en_US dataset
 *    by champion id — a shared URL must mean the same thing in every locale;
 *  - attack range is bimodal but not cleanly: Nilah 225, Rakan 300, Lillia 325
 *    are melee, Urgot 350 is ranged (Riot's own classification), hence the
 *    325/350 cut rather than the 200/425 gap.
 */
final class ChampionFacets implements FacetSchemaInterface
{
    private const ROLES = ['Fighter', 'Tank', 'Mage', 'Assassin', 'Marksman', 'Support'];
    private const TOKEN_LANG = 'en_US';
    private const NO_RESOURCE = 'none';
    private const MELEE_MAX_RANGE = 325;
    private const RANGE_MELEE = 'melee';
    private const RANGE_RANGED = 'ranged';
    private const RATINGS = ['difficulty', 'attack', 'defense', 'magic'];
    /** facet key => [DDragon stats key, GameStat] — level-1 base values. */
    private const BASE_STATS = [
        'hp'    => ['hp', GameStat::Health],
        'armor' => ['armor', GameStat::Armor],
        'mr'    => ['spellblock', GameStat::MagicResist],
        'ad'    => ['attackdamage', GameStat::AttackDamage],
        'as'    => ['attackspeed', GameStat::AttackSpeed],
        'ms'    => ['movespeed', GameStat::MoveSpeed],
    ];
    private const ATTACK_SPEED_STEP = 0.01;

    public function __construct(
        private readonly ChampionManager $champions,
        private readonly TranslatorInterface $translator,
    ) {}

    public function type(): string
    {
        return 'champion';
    }

    public function schema(DatasetRef $ref): array
    {
        return [
            $this->profileChoice('role', $this->roleOptions()),
            $this->profileChoice('resource', $this->resourceOptions($ref)),
            $this->profileChoice('range', [
                $this->option(self::RANGE_MELEE, 'facet.champion.ranges.melee'),
                $this->option(self::RANGE_RANGED, 'facet.champion.ranges.ranged'),
            ]),
            ...array_map(fn (string $key): FacetDefinition => $this->rating($key), self::RATINGS),
            ...array_map(
                fn (string $key): FacetDefinition => $this->baseStat($key),
                array_keys(self::BASE_STATS),
            ),
        ];
    }

    public function valuesOf(string $id, array $entry, DatasetRef $ref): array
    {
        $values = [
            'role'     => array_values(array_map(strval(...), $entry['tags'] ?? [])),
            'resource' => $this->resourceToken($id, $entry, $ref),
        ];
        $range = self::rangeBucket($entry['stats']['attackrange'] ?? null);
        if ($range !== null) {
            $values['range'] = $range;
        }
        foreach (self::RATINGS as $key) {
            if (isset($entry['info'][$key])) {
                $values[$key] = (int) $entry['info'][$key];
            }
        }
        foreach (self::BASE_STATS as $key => [$statKey]) {
            if (isset($entry['stats'][$statKey])) {
                $values[$key] = round((float) $entry['stats'][$statKey], 3);
            }
        }

        return $values;
    }

    /** @return list<array{value: string, label: string}> */
    private function roleOptions(): array
    {
        return array_map(
            fn (string $role): array => $this->option($role, 'facet.champion.roles.'.strtolower($role)),
            self::ROLES,
        );
    }

    /**
     * Tokens present in the patch, labelled from the reader's own locale, most
     * common first (Mana, then Energy…).
     *
     * @return list<array{value: string, label: string}>
     */
    private function resourceOptions(DatasetRef $ref): array
    {
        $labels = [];
        $counts = [];
        $localized = $this->champions->getData($ref->version, $ref->lang)['data'] ?? [];
        foreach ($localized as $id => $entry) {
            $token = $this->resourceToken((string) $id, $entry, $ref);
            $counts[$token] = ($counts[$token] ?? 0) + 1;
            $labels[$token] ??= $token === self::NO_RESOURCE
                ? $this->translator->trans('facet.champion.resource_none')
                : trim((string) ($entry['partype'] ?? $token));
        }
        arsort($counts);

        return array_map(
            static fn (string $token): array => ['value' => $token, 'label' => $labels[$token]],
            array_keys($counts),
        );
    }

    /** @param array<mixed> $entry */
    private function resourceToken(string $id, array $entry, DatasetRef $ref): string
    {
        $reference = $this->champions->getData($ref->version, self::TOKEN_LANG)['data'][$id] ?? $entry;

        return self::resourceSlug((string) ($reference['partype'] ?? ''));
    }

    private static function resourceSlug(string $partype): string
    {
        $slug = trim((string) preg_replace('/[^a-z0-9]+/', '-', strtolower(trim($partype))), '-');

        return $slug === '' || $slug === self::NO_RESOURCE ? self::NO_RESOURCE : $slug;
    }

    private static function rangeBucket(mixed $attackRange): ?string
    {
        if (!is_numeric($attackRange)) {
            return null;
        }

        return (float) $attackRange <= self::MELEE_MAX_RANGE ? self::RANGE_MELEE : self::RANGE_RANGED;
    }

    private function rating(string $key): FacetDefinition
    {
        return new FacetDefinition(
            key: $key,
            kind: FacetKind::Range,
            label: $this->translator->trans('facet.champion.'.$key),
            group: $this->translator->trans('facet.group.ratings'),
        );
    }

    private function baseStat(string $key): FacetDefinition
    {
        [, $stat] = self::BASE_STATS[$key];

        return new FacetDefinition(
            key: $key,
            kind: FacetKind::Range,
            label: $this->translator->trans($stat->labelKey()),
            group: $this->translator->trans('facet.group.base_stats'),
            step: $stat === GameStat::AttackSpeed ? self::ATTACK_SPEED_STEP : 1.0,
        );
    }

    /**
     * The inline "profile" chips — every choice facet of a champion is one.
     *
     * @param list<array{value: string, label: string}> $options
     */
    private function profileChoice(string $key, array $options): FacetDefinition
    {
        return new FacetDefinition(
            key: $key,
            kind: FacetKind::Choice,
            label: $this->translator->trans('facet.champion.'.$key),
            group: $this->translator->trans('facet.group.profile'),
            options: $options,
            isPrimary: true,
        );
    }

    /** @return array{value: string, label: string} */
    private function option(string $value, string $labelKey): array
    {
        return ['value' => $value, 'label' => $this->translator->trans($labelKey)];
    }
}
