<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * Encoding primitive shared by every schema.org node builder.
 *
 * Encoding is the security boundary: JSON_HEX_TAG hex-escapes every angle
 * bracket, so data (names, descriptions) can never close the surrounding
 * <script type="application/ld+json"> element and inject markup. Slashes stay
 * unescaped for readable URLs; unicode stays verbatim so the 21 locales keep
 * their native script.
 */
final class JsonLdEncoder
{
    public const SCHEMA_CONTEXT = 'https://schema.org';

    private const ENCODE_FLAGS = JSON_HEX_TAG | JSON_HEX_AMP | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE;

    /** @param array<string,mixed> $data */
    public function encode(array $data): string
    {
        return json_encode($data, self::ENCODE_FLAGS | JSON_THROW_ON_ERROR);
    }

    /**
     * Drops null / empty-string / empty-array values so optional fields are
     * absent rather than emitted empty — an empty schema.org value is invalid,
     * absence is not. Shallow by design: nested nodes are pruned by their own
     * builder before being passed in.
     *
     * @param array<string,mixed> $node
     * @return array<string,mixed>
     */
    public function prune(array $node): array
    {
        return array_filter($node, static fn (mixed $value): bool => $value !== null && $value !== '' && $value !== []);
    }

    /**
     * Wraps nodes into a single linked `@graph` document. One @context for the
     * whole set is what lets consumers resolve the `@id` cross-references
     * between the nodes instead of reading them as unrelated fragments.
     *
     * @param list<array<string,mixed>> $nodes
     * @return array<string,mixed>
     */
    public function graph(array $nodes): array
    {
        return [
            '@context' => self::SCHEMA_CONTEXT,
            '@graph'   => array_values($nodes),
        ];
    }
}
