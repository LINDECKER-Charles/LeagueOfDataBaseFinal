<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig\Codex;

use App\Service\API\DatasetRef;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Catalog\Facet\FacetKind;
use App\Service\Catalog\Facet\FacetSchemaInterface;
use App\Service\Catalog\Facet\FacetSchemaRegistry;
use App\Service\Client\PageSelectionInterface;
use App\Twig\Codex\FacetExtension;
use PHPUnit\Framework\TestCase;

/**
 * The attribute string a card carries: only valued facets, lists joined on
 * `|`, flags as `1`, values escaped — and the schema serialised for the island.
 */
final class FacetExtensionTest extends TestCase
{
    public function testRendersValuedFacetsAsEscapedDataAttributes(): void
    {
        $extension = $this->extension([
            'tag' => ['Boots', 'Armor'],
            'edition' => 'modern',
            'name' => 'Doran\'s "Blade" <3',
            'price' => 300,
            'purchasable' => true,
            'consumable' => false,
            'map' => [],
        ]);

        self::assertSame(
            'data-f-tag="Boots|Armor" data-f-edition="modern" '
            .'data-f-name="Doran&#039;s &quot;Blade&quot; &lt;3" data-f-price="300" data-f-purchasable="1"',
            $extension->attributes('item', 1001, []),
        );
    }

    public function testSerialisesTheSchemaForTheIsland(): void
    {
        $schema = $this->extension([])->schema('item');

        self::assertCount(1, $schema);
        self::assertSame('tag', $schema[0]['key']);
        self::assertSame('choice', $schema[0]['kind']);
        self::assertTrue($schema[0]['matchAll']);
    }

    /** @param array<string, mixed> $values */
    private function extension(array $values): FacetExtension
    {
        $schema = new class($values) implements FacetSchemaInterface {
            /** @param array<string, mixed> $values */
            public function __construct(private readonly array $values) {}

            public function type(): string
            {
                return 'item';
            }

            public function schema(DatasetRef $ref): array
            {
                return [new FacetDefinition(
                    key: 'tag', kind: FacetKind::Choice, label: 'Tag', group: 'Category', canMatchAll: true,
                )];
            }

            public function valuesOf(string $id, array $entry, DatasetRef $ref): array
            {
                return $this->values;
            }
        };
        $context = $this->createStub(PageSelectionInterface::class);
        $context->method('selection')->willReturn(['version' => '16.16.1', 'lang' => 'en_US']);

        return new FacetExtension(new FacetSchemaRegistry([$schema]), $context);
    }
}
