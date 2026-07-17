<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * Builds schema.org structured-data nodes as plain arrays and encodes them for
 * embedding inside a <script type="application/ld+json"> element.
 *
 * Encoding is the security boundary: JSON_HEX_TAG hex-escapes every angle
 * bracket, so data (names, descriptions) can never close the script element
 * and inject markup. Slashes stay unescaped for readable URLs; unicode stays
 * verbatim so the 21 locales keep their native script.
 */
final class JsonLdBuilder
{
    public const SCHEMA_CONTEXT = 'https://schema.org';

    /** The game every entity belongs to — a product name, not a translatable string. */
    public const GAME_NAME = 'League of Legends';

    /** Enough entries for rich results without bloating the render of 600-item lists. */
    public const ITEM_LIST_MAX = 20;

    private const ENCODE_FLAGS = JSON_HEX_TAG | JSON_HEX_AMP | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE;

    /** @param array<string,mixed> $data */
    public function encode(array $data): string
    {
        return json_encode($data, self::ENCODE_FLAGS | JSON_THROW_ON_ERROR);
    }

    /** @return array<string,mixed> */
    public function webSite(string $name, string $url): array
    {
        return [
            '@context' => self::SCHEMA_CONTEXT,
            '@type'    => 'WebSite',
            'name'     => $name,
            'url'      => $url,
        ];
    }

    /** @return array<string,mixed> */
    public function organization(string $name, string $url, string $logoUrl): array
    {
        return [
            '@context' => self::SCHEMA_CONTEXT,
            '@type'    => 'Organization',
            'name'     => $name,
            'url'      => $url,
            'logo'     => $logoUrl,
        ];
    }

    /**
     * @param list<array{name:string, url:string}> $crumbs ordered root → current page
     * @return array<string,mixed>
     */
    public function breadcrumbList(array $crumbs): array
    {
        $elements = [];
        foreach (array_values($crumbs) as $i => $crumb) {
            $elements[] = [
                '@type'    => 'ListItem',
                'position' => $i + 1,
                'name'     => (string) ($crumb['name'] ?? ''),
                'item'     => (string) ($crumb['url'] ?? ''),
            ];
        }

        return [
            '@context'        => self::SCHEMA_CONTEXT,
            '@type'           => 'BreadcrumbList',
            'itemListElement' => $elements,
        ];
    }

    /**
     * @param list<array{name:string, url:string}> $items capped to {@see self::ITEM_LIST_MAX}
     * @return array<string,mixed>
     */
    public function itemList(array $items): array
    {
        $elements = [];
        foreach (array_values(\array_slice($items, 0, self::ITEM_LIST_MAX)) as $i => $item) {
            $elements[] = [
                '@type'    => 'ListItem',
                'position' => $i + 1,
                'name'     => (string) ($item['name'] ?? ''),
                'url'      => (string) ($item['url'] ?? ''),
            ];
        }

        return [
            '@context'        => self::SCHEMA_CONTEXT,
            '@type'           => 'ItemList',
            'numberOfItems'   => \count($elements),
            'itemListElement' => $elements,
        ];
    }

    /**
     * VideoGame node exposing a champion as a playable character (Person).
     *
     * @return array<string,mixed>
     */
    public function gameCharacter(string $name, ?string $imageUrl = null, ?string $description = null): array
    {
        return $this->videoGame('character', $this->entity('Person', $name, $imageUrl, $description));
    }

    /**
     * VideoGame node exposing an item / rune path / summoner spell as in-game content.
     *
     * @return array<string,mixed>
     */
    public function gameItem(string $name, ?string $imageUrl = null, ?string $description = null): array
    {
        return $this->videoGame('gameItem', $this->entity('Thing', $name, $imageUrl, $description));
    }

    /**
     * @param array<string,mixed> $entity
     * @return array<string,mixed>
     */
    private function videoGame(string $property, array $entity): array
    {
        return [
            '@context' => self::SCHEMA_CONTEXT,
            '@type'    => 'VideoGame',
            'name'     => self::GAME_NAME,
            $property  => $entity,
        ];
    }

    /**
     * Optional fields are omitted rather than emitted empty — an empty "image"
     * is invalid structured data, absence is not.
     *
     * @return array<string,mixed>
     */
    private function entity(string $type, string $name, ?string $imageUrl, ?string $description): array
    {
        $node = ['@type' => $type, 'name' => $name];
        if ($imageUrl !== null && $imageUrl !== '') {
            $node['image'] = $imageUrl;
        }
        if ($description !== null && trim($description) !== '') {
            $node['description'] = trim($description);
        }

        return $node;
    }
}
