<?php
declare(strict_types=1);

namespace App\Service\API;

/**
 * The Data Dragon dataset a render reads from: a patch version and the language
 * its labels come in.
 *
 * The two never travel apart — together they key every dataset payload, every
 * storage path and every cache entry of the resource managers — so they are
 * carried as one value instead of a `(string $version, string $lang)` pair
 * threaded through every signature. Built once at the manager boundary, from the
 * selection resolved for the current page
 * ({@see \App\Service\Client\PageContextResolver::selection()}), then passed
 * down unchanged.
 *
 * Images are deliberately NOT addressed by this pair: they are language-agnostic
 * and keyed by version alone, which is why the ingestion/manifest layer keeps
 * taking a bare version.
 */
final readonly class DatasetRef
{
    public function __construct(
        public string $version,
        public string $lang,
    ) {}

    /**
     * Adopt the version/language selection resolved for the current page.
     *
     * @param array{version: string, lang: string} $selection
     */
    public static function fromSelection(array $selection): self
    {
        return new self($selection['version'], $selection['lang']);
    }

    /** Same patch read in another language — backs the Data Dragon locale fallback. */
    public function withLang(string $lang): self
    {
        return new self($this->version, $lang);
    }
}
