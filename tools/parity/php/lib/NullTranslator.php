<?php
declare(strict_types=1);

namespace LoDbParity;

use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * The facet schemas take a translator for their labels; the values the export reads
 * (valuesOf) never translate anything.
 */
final class NullTranslator implements TranslatorInterface
{
    public function trans(
        string $id,
        array $parameters = [],
        ?string $domain = null,
        ?string $locale = null,
    ): string {
        return $id;
    }

    public function getLocale(): string
    {
        return LegacyCatalog::FALLBACK_LANG;
    }
}
