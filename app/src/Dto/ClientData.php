<?php
declare(strict_types=1);

namespace App\Dto;

use App\Service\ClientManager;
use App\Service\VersionManager;

/**
 * Données client transverses (utilisables sur toutes les pages).
 * Contient uniquement :
 * - versions          : liste des versions DDragon
 * - languages         : liste des langues DDragon
 * - languageLabels    : libellés lisibles par langue
 * - currentLocale     : langue du navigateur (BCP47 normalisée, ex. "fr_FR")
 * - session           : préférences { locale:?string, version:?string } hydratées depuis session/cookie
 */
final class ClientData
{
    /**
     * @param string[]             $versions
     * @param string[]             $languages
     * @param array<string,string> $languageLabels
     * @param array{locale:?string, version:?string} $session
     */
    public function __construct(
        public readonly array  $versions,
        public readonly array  $languages,
        public readonly array  $languageLabels,
        public readonly string $currentLocale,
        public readonly array  $session,
    ) {}

    /**
     * Construit un ClientData depuis les services applicatifs.
     */
    public static function fromServices(VersionManager $vm, ClientManager $cm): self
    {
        return new self(
            versions:       $vm->getVersions(),
            languages:      $vm->getLanguages(),
            languageLabels: $vm->getLanguageLabels(),
            currentLocale:  $cm->getLangue(),
            session:        $cm->getOrHydratePreferences(),
        );
    }
}
