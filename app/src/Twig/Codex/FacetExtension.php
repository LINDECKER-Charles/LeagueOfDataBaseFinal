<?php
declare(strict_types=1);

namespace App\Twig\Codex;

use App\Service\API\DatasetRef;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Catalog\Facet\FacetSchemaRegistry;
use App\Service\Client\PageSelectionInterface;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * The list templates' face of the facet schemas: the schema the filter island
 * is handed as props, and the `data-f-<key>` attributes each card carries.
 * Templates never compute a facet value themselves — they echo what the
 * schema says about the raw Data Dragon node, for the dataset being rendered
 * ({@see PageSelectionInterface::selection()}, the same resolution the controller used).
 */
final class FacetExtension extends AbstractExtension
{
    private const ATTRIBUTE_PREFIX = 'data-f-';
    private const LIST_SEPARATOR = '|';

    public function __construct(
        private readonly FacetSchemaRegistry $schemas,
        private readonly PageSelectionInterface $pageContext,
    ) {}

    public function getFunctions(): array
    {
        return [
            new TwigFunction('facet_schema', $this->schema(...)),
            new TwigFunction('facet_attrs', $this->attributes(...), ['is_safe' => ['html']]),
        ];
    }

    /** @return list<array<string,mixed>> */
    public function schema(string $type): array
    {
        return array_map(
            static fn (FacetDefinition $facet): array => $facet->toArray(),
            $this->schemas->get($type)->schema($this->dataset()),
        );
    }

    /**
     * Attribute string of one card — only the facets the entry has a value for,
     * lists joined on `|`, booleans as `1`, everything escaped.
     *
     * @param array<mixed> $entry
     */
    public function attributes(string $type, string|int $id, array $entry): string
    {
        $attributes = [];
        foreach ($this->schemas->get($type)->valuesOf((string) $id, $entry, $this->dataset()) as $key => $value) {
            $text = self::attributeValue($value);
            if ($text === null) {
                continue;
            }
            $attributes[] = sprintf(
                '%s%s="%s"',
                self::ATTRIBUTE_PREFIX,
                $key,
                htmlspecialchars($text, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8'),
            );
        }

        return implode(' ', $attributes);
    }

    /** @param string|list<string>|int|float|bool $value */
    private static function attributeValue(string|array|int|float|bool $value): ?string
    {
        if (\is_array($value)) {
            return $value === [] ? null : implode(self::LIST_SEPARATOR, $value);
        }
        if (\is_bool($value)) {
            return $value ? '1' : null;
        }

        return (string) $value;
    }

    private function dataset(): DatasetRef
    {
        return DatasetRef::fromSelection($this->pageContext->selection());
    }
}
