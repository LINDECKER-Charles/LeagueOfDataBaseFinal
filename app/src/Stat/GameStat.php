<?php
declare(strict_types=1);

namespace App\Stat;

/**
 * Canonical catalogue of the game attributes surfaced across the encyclopedia
 * (champion base stats, item stat blocks). Single source of truth for a stat's
 * i18n label key, its icon, and how Data Dragon's item `stats` keys map onto it.
 *
 * `icon()` returns null while no artwork is bundled for a stat yet — Data Dragon
 * only ships individual icons for the rune stat-shards, so mana, regen, range,
 * crit and life steal currently render label-only. Adding one later is a single
 * entry in {@see self::WITH_ICON} plus the matching file under public/icons/stats.
 */
enum GameStat: string
{
    case AttackDamage = 'attack_damage';
    case AbilityPower = 'ability_power';
    case AttackSpeed  = 'attack_speed';
    case CritChance   = 'crit_chance';
    case LifeSteal    = 'life_steal';
    case Health       = 'health';
    case HealthRegen  = 'health_regen';
    case Armor        = 'armor';
    case MagicResist  = 'magic_resist';
    case Mana         = 'mana';
    case ManaRegen    = 'mana_regen';
    case MoveSpeed    = 'move_speed';
    case AttackRange  = 'attack_range';

    /** Stats that have bundled CommunityDragon artwork today. */
    private const WITH_ICON = [
        'attack_damage',
        'ability_power',
        'attack_speed',
        'health',
        'armor',
        'magic_resist',
        'move_speed',
    ];

    /**
     * Data Dragon item `stats` key => [stat, isPercent]. Declaration order is the
     * display order. Percent-encoded keys are stored by Data Dragon as fractions
     * (0.25 = 25 %); crit chance uses a `Flat…` key despite being a percentage.
     */
    private const ITEM_KEYS = [
        'FlatPhysicalDamageMod'   => [self::AttackDamage, false],
        'FlatMagicDamageMod'      => [self::AbilityPower, false],
        'PercentAttackSpeedMod'   => [self::AttackSpeed, true],
        'FlatCritChanceMod'       => [self::CritChance, true],
        'PercentLifeStealMod'     => [self::LifeSteal, true],
        'FlatHPPoolMod'           => [self::Health, false],
        'FlatHPRegenMod'          => [self::HealthRegen, false],
        'FlatArmorMod'            => [self::Armor, false],
        'FlatSpellBlockMod'       => [self::MagicResist, false],
        'FlatMPPoolMod'           => [self::Mana, false],
        'FlatMovementSpeedMod'    => [self::MoveSpeed, false],
        'PercentMovementSpeedMod' => [self::MoveSpeed, true],
    ];

    public function labelKey(): string
    {
        return 'stat.'.$this->value;
    }

    /** Public path of the 32×32 icon, or null when no artwork is bundled yet. */
    public function icon(): ?string
    {
        return in_array($this->value, self::WITH_ICON, true)
            ? '/icons/stats/'.$this->value.'.png'
            : null;
    }

    /**
     * Flatten a Data Dragon item `stats` block into ordered, non-zero rows.
     *
     * @param array<string,int|float>|null $stats
     *
     * @return list<array{stat: self, percent: bool, value: float}>
     */
    public static function fromItemStats(?array $stats): array
    {
        if (!$stats) {
            return [];
        }

        $rows = [];
        foreach (self::ITEM_KEYS as $key => [$stat, $percent]) {
            $value = (float) ($stats[$key] ?? 0);
            if ($value === 0.0) {
                continue;
            }
            $rows[] = ['stat' => $stat, 'percent' => $percent, 'value' => $value];
        }

        return $rows;
    }
}
