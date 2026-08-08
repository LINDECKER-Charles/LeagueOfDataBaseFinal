<?php
declare(strict_types=1);

namespace App\Service\Seo\JsonLd;

/**
 * Projects a Data Dragon entity onto a schema.org VideoGame node whose nested
 * character / gameItem carries the entity's *actual* values as PropertyValue
 * entries (champion base stats, item gold cost, spell cooldown…).
 *
 * Why PropertyValue rather than richer types: schema.org has no vocabulary for
 * game statistics, and an in-game item priced in gold is not a Product with an
 * Offer (no real currency, nothing purchasable). PropertyValue with an explicit
 * unitText states the same facts without claiming a semantic we cannot honour.
 *
 * Input stays as the raw Data Dragon arrays: this is the boundary with an
 * external, schema-less feed, and every field is read defensively — a patch
 * that drops a key degrades the node instead of breaking the page.
 */
final class GameEntityJsonLd
{
    /** The game every entity belongs to — a product name, not a translatable string. */
    public const GAME_NAME = 'League of Legends';

    /** Rights holder of the underlying game data; states provenance to consumers. */
    private const GAME_PUBLISHER = 'Riot Games';

    private const PLATFORM = 'PC';

    private const UNIT_GOLD = 'gold';
    private const UNIT_SECONDS = 'seconds';
    private const UNIT_UNITS = 'units';

    /**
     * Base-level champion stats worth stating, mapped to canonical English
     * labels. The `*perlevel` growth fields are excluded on purpose: they
     * describe a curve, not a fact that reads usefully out of context.
     */
    private const CHAMPION_STATS = [
        'hp'           => 'Health',
        'armor'        => 'Armor',
        'spellblock'   => 'Magic resist',
        'attackdamage' => 'Attack damage',
        'attackspeed'  => 'Attack speed',
        'movespeed'    => 'Move speed',
        'attackrange'  => 'Attack range',
    ];

    /** Riot's own 0-10 profile ratings — the closest thing to a play-style summary. */
    private const CHAMPION_RATINGS = [
        'attack'     => 'Attack rating',
        'defense'    => 'Defense rating',
        'magic'      => 'Magic rating',
        'difficulty' => 'Difficulty',
    ];

    private const ITEM_GOLD = [
        'total' => 'Total cost',
        'base'  => 'Combine cost',
        'sell'  => 'Sell value',
    ];

    public function __construct(
        private readonly JsonLdEncoder $encoder,
    ) {}

    /**
     * Champion → VideoGame.character (Person): identity, lore blurb, role tags,
     * resource type, Riot profile ratings and base stats.
     *
     * @param array<string,mixed> $champion raw Data Dragon champion entry
     * @return array<string,mixed>
     */
    public function champion(array $champion, ?string $imageUrl, string $url): array
    {
        $properties = array_merge(
            $this->listProperty('Role', $champion['tags'] ?? null),
            $this->scalarProperty('Resource', $champion['partype'] ?? null),
            $this->mapped($champion['info'] ?? null, self::CHAMPION_RATINGS),
            $this->mapped($champion['stats'] ?? null, self::CHAMPION_STATS),
        );

        $character = $this->entity('Person', $url . '#character', [
            'name'          => $this->text($champion['name'] ?? ''),
            'alternateName' => $this->text($champion['title'] ?? null),
            'image'         => $imageUrl,
            'description'   => $this->text($champion['blurb'] ?? $champion['lore'] ?? null),
            'additionalProperty' => $properties,
        ]);

        return $this->videoGame('character', $character, $url);
    }

    /**
     * Item → VideoGame.gameItem: gold economy (cost / combine / sell), category
     * tags and purchasability.
     *
     * @param array<string,mixed> $item raw Data Dragon item entry
     * @return array<string,mixed>
     */
    public function item(array $item, ?string $imageUrl, string $url): array
    {
        $gold = \is_array($item['gold'] ?? null) ? $item['gold'] : [];

        $properties = array_merge(
            $this->mapped($gold, self::ITEM_GOLD, self::UNIT_GOLD),
            $this->listProperty('Category', $item['tags'] ?? null),
            isset($gold['purchasable']) && $gold['purchasable'] === false
                ? $this->scalarProperty('Purchasable', 'false')
                : [],
        );

        $entity = $this->entity('Thing', $url . '#item', [
            'name'        => $this->text($item['name'] ?? ''),
            'image'       => $imageUrl,
            'description' => $this->text($item['plaintext'] ?? $item['description'] ?? null),
            'additionalProperty' => $properties,
        ]);

        return $this->videoGame('gameItem', $entity, $url);
    }

    /**
     * Rune path → VideoGame.gameItem, stating how many slots the path carries
     * (its defining structure) alongside identity.
     *
     * @param array<string,mixed> $rune raw Data Dragon runesReforged path
     * @return array<string,mixed>
     */
    public function runePath(array $rune, ?string $imageUrl, string $url): array
    {
        $slots = \is_array($rune['slots'] ?? null) ? $rune['slots'] : [];

        $entity = $this->entity('Thing', $url . '#rune-path', [
            'name'  => $this->text($rune['name'] ?? ''),
            'image' => $imageUrl,
            'additionalProperty' => $this->scalarProperty(
                'Slots',
                $slots === [] ? null : \count($slots),
            ),
        ]);

        return $this->videoGame('gameItem', $entity, $url);
    }

    /**
     * Summoner spell → VideoGame.gameItem: cooldown, range and unlock level —
     * the three values that actually distinguish one spell from another.
     *
     * @param array<string,mixed> $spell raw Data Dragon summoner entry
     * @return array<string,mixed>
     */
    public function summonerSpell(array $spell, ?string $imageUrl, string $url): array
    {
        $properties = array_merge(
            $this->scalarProperty(
                'Cooldown',
                $this->firstNumber($spell['cooldown'] ?? null),
                self::UNIT_SECONDS,
            ),
            $this->scalarProperty(
                'Range',
                $this->firstNumber($spell['range'] ?? null),
                self::UNIT_UNITS,
            ),
            $this->scalarProperty('Required level', $spell['summonerLevel'] ?? null),
        );

        $entity = $this->entity('Thing', $url . '#spell', [
            'name'        => $this->text($spell['name'] ?? ''),
            'image'       => $imageUrl,
            'description' => $this->text(strip_tags((string) ($spell['description'] ?? ''))),
            'additionalProperty' => $properties,
        ]);

        return $this->videoGame('gameItem', $entity, $url);
    }

    /**
     * @param array<string,mixed> $fields identity fields, `additionalProperty` included
     * @return array<string,mixed>
     */
    private function entity(string $type, string $id, array $fields): array
    {
        return $this->encoder->prune(['@type' => $type, '@id' => $id, ...$fields]);
    }

    /**
     * @param array<string,mixed> $entity
     * @return array<string,mixed>
     */
    private function videoGame(string $property, array $entity, string $url): array
    {
        return [
            '@context'     => JsonLdEncoder::SCHEMA_CONTEXT,
            '@type'        => 'VideoGame',
            '@id'          => $url . '#game',
            'name'         => self::GAME_NAME,
            'url'          => $url,
            'gamePlatform' => self::PLATFORM,
            'publisher'    => ['@type' => 'Organization', 'name' => self::GAME_PUBLISHER],
            $property      => $entity,
        ];
    }

    /**
     * Picks the labelled keys out of a Data Dragon sub-array, in map order.
     *
     * @param mixed $source expected array, anything else yields no properties
     * @param array<string,string> $labels source key => canonical label
     * @return list<array<string,mixed>>
     */
    private function mapped(mixed $source, array $labels, ?string $unit = null): array
    {
        if (!\is_array($source)) {
            return [];
        }

        $properties = [];
        foreach ($labels as $key => $label) {
            $properties = array_merge(
                $properties,
                $this->scalarProperty($label, $source[$key] ?? null, $unit),
            );
        }

        return $properties;
    }

    /**
     * One PropertyValue, or nothing when the value is absent — emitting an empty
     * property is worse than omitting it.
     *
     * @return list<array<string,mixed>>
     */
    private function scalarProperty(string $name, mixed $value, ?string $unit = null): array
    {
        if ($value === null || $value === '' || (\is_float($value) && is_nan($value))) {
            return [];
        }

        return [$this->encoder->prune([
            '@type'    => 'PropertyValue',
            'name'     => $name,
            'value'    => \is_bool($value) ? ($value ? 'true' : 'false') : $value,
            'unitText' => $unit,
        ])];
    }

    /**
     * Repeats a property once per list entry (a champion has several roles, an
     * item several categories) — the shape consumers expect for multi-values.
     *
     * @return list<array<string,mixed>>
     */
    private function listProperty(string $name, mixed $values): array
    {
        if (!\is_array($values)) {
            return [];
        }

        $properties = [];
        foreach ($values as $value) {
            if (\is_scalar($value)) {
                $properties = array_merge($properties, $this->scalarProperty($name, $value));
            }
        }

        return $properties;
    }

    /** Data Dragon wraps per-rank values in arrays; the rank-1 entry is the fact to state. */
    private function firstNumber(mixed $values): int|float|null
    {
        $first = \is_array($values) ? reset($values) : $values;

        return \is_int($first) || \is_float($first) ? $first : null;
    }

    /** Trims to a usable string, or null when there is nothing to say. */
    private function text(mixed $value): ?string
    {
        if (!\is_string($value)) {
            return null;
        }

        $trimmed = trim($value);

        return $trimmed === '' ? null : $trimmed;
    }
}
