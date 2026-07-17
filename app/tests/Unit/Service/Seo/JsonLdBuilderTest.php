<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo;

use App\Service\Seo\JsonLdBuilder;
use PHPUnit\Framework\TestCase;

final class JsonLdBuilderTest extends TestCase
{
    private JsonLdBuilder $builder;

    protected function setUp(): void
    {
        $this->builder = new JsonLdBuilder();
    }

    public function testEncodeEscapesAngleBracketsAgainstScriptBreakout(): void
    {
        $payload = '</script><script>alert(1)</script>';

        $json = $this->builder->encode(['description' => $payload]);

        // No raw "<" may survive (it could close the ld+json script element),
        // yet decoding must restore the exact payload — escaped, not stripped.
        self::assertStringNotContainsString('<', $json);
        self::assertSame($payload, json_decode($json, true)['description']);
    }

    public function testEncodeKeepsSlashesAndUnicodeReadable(): void
    {
        $json = $this->builder->encode(['url' => 'https://example.com/champion/Aatrox', 'name' => 'Ré?']);

        self::assertStringContainsString('https://example.com/champion/Aatrox', $json);
        self::assertStringContainsString('Ré?', $json);
    }

    public function testBreadcrumbListPositionsAreOrdered(): void
    {
        $graph = $this->builder->breadcrumbList([
            ['name' => 'Home', 'url' => 'https://example.com/'],
            ['name' => 'Champions', 'url' => 'https://example.com/champions'],
            ['name' => 'Aatrox', 'url' => 'https://example.com/champion/Aatrox'],
        ]);

        self::assertSame(JsonLdBuilder::SCHEMA_CONTEXT, $graph['@context']);
        self::assertSame('BreadcrumbList', $graph['@type']);
        self::assertSame([1, 2, 3], array_column($graph['itemListElement'], 'position'));
        self::assertSame('Aatrox', $graph['itemListElement'][2]['name']);
        self::assertSame('https://example.com/champion/Aatrox', $graph['itemListElement'][2]['item']);
    }

    public function testItemListCapsEntriesAtMax(): void
    {
        $items = array_map(
            static fn (int $i): array => ['name' => "Item $i", 'url' => "https://example.com/object/$i"],
            range(1, JsonLdBuilder::ITEM_LIST_MAX + 15),
        );

        $graph = $this->builder->itemList($items);

        self::assertSame('ItemList', $graph['@type']);
        self::assertCount(JsonLdBuilder::ITEM_LIST_MAX, $graph['itemListElement']);
        self::assertSame(JsonLdBuilder::ITEM_LIST_MAX, $graph['numberOfItems']);
        self::assertSame('Item 1', $graph['itemListElement'][0]['name']);
    }

    public function testGameCharacterWrapsAPersonInsideTheVideoGame(): void
    {
        $graph = $this->builder->gameCharacter('Aatrox', 'https://example.com/cdn/aatrox.png', 'The Darkin Blade.');

        self::assertSame('VideoGame', $graph['@type']);
        self::assertSame(JsonLdBuilder::GAME_NAME, $graph['name']);
        self::assertSame(
            ['@type' => 'Person', 'name' => 'Aatrox', 'image' => 'https://example.com/cdn/aatrox.png', 'description' => 'The Darkin Blade.'],
            $graph['character'],
        );
    }

    public function testGameItemOmitsEmptyOptionalFields(): void
    {
        $graph = $this->builder->gameItem('Flash', null, '   ');

        self::assertSame(['@type' => 'Thing', 'name' => 'Flash'], $graph['gameItem']);
    }

    public function testWebSiteAndOrganizationCarryTheirOwnContext(): void
    {
        $site = $this->builder->webSite('LODB', 'https://example.com/');
        $org  = $this->builder->organization('LODB', 'https://example.com/', 'https://example.com/logo.png');

        self::assertSame(['WebSite', 'Organization'], [$site['@type'], $org['@type']]);
        self::assertSame(JsonLdBuilder::SCHEMA_CONTEXT, $site['@context']);
        self::assertSame('https://example.com/logo.png', $org['logo']);
    }
}
