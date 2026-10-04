<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet;

use Symfony\Component\DependencyInjection\Attribute\AutowireIterator;

/** Resource type => facet schema, fed by the `app.facet_schema` tag. */
final class FacetSchemaRegistry
{
    /** @var array<string,FacetSchemaInterface> */
    private readonly array $schemas;

    /** @param iterable<FacetSchemaInterface> $schemas */
    public function __construct(
        #[AutowireIterator('app.facet_schema')]
        iterable $schemas,
    ) {
        $byType = [];
        foreach ($schemas as $schema) {
            $byType[$schema->type()] = $schema;
        }
        $this->schemas = $byType;
    }

    /** @throws \InvalidArgumentException for a type without a schema */
    public function get(string $type): FacetSchemaInterface
    {
        return $this->schemas[$type]
            ?? throw new \InvalidArgumentException(sprintf('No facet schema for "%s".', $type));
    }
}
