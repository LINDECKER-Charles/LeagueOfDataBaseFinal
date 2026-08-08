<?php
declare(strict_types=1);

namespace App\Dto;

use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;

/**
 * Immutable snapshot of the version/language context handed to every view: built
 * once per request so no template has to re-query the managers, and frozen so a
 * late mutation cannot make two fragments of the same page disagree.
 */
final class ClientData
{
    /**
     * @param string[]             $versions       DDragon versions, newest first
     * @param string[]             $languages      DDragon language codes
     * @param array<string,string> $languageLabels human-readable label per language
     * @param string               $currentLocale  resolved UI locale (BCP47, e.g. "fr_FR")
     * @param array{locale:?string, version:?string} $session preferences from session/cookie
     */
    public function __construct(
        public readonly array  $versions,
        public readonly array  $languages,
        public readonly array  $languageLabels,
        public readonly string $currentLocale,
        public readonly array  $session,
    ) {}

    public static function fromServices(VersionManager $versions, ClientManager $client): self
    {
        return new self(
            versions:       $versions->getVersions(),
            languages:      $versions->getLanguages(),
            languageLabels: $versions->getLanguageLabels(),
            currentLocale:  $client->getLangue(),
            session:        $client->getOrHydratePreferences(),
        );
    }
}
