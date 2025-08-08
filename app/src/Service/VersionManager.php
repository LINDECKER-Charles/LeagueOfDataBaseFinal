<?php

namespace App\Service;

use Symfony\Contracts\HttpClient\HttpClientInterface;
use Psr\Log\LoggerInterface;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

class VersionManager
{
    private const RIOT_VERSIONS_URL = 'https://ddragon.leagueoflegends.com/api/versions.json';

    public function __construct(
        private readonly HttpClientInterface $httpClient,
        private readonly CacheInterface $cache,
        private readonly LoggerInterface $logger
    ) {}

    /**
     * Récupère la liste complète des versions depuis Riot (Data Dragon)
     *
     * @return array Liste des versions, la première étant la plus récente
     */
    public function getVersions(): array
    {
        return $this->cache->get('riot_versions', function (ItemInterface $item) {
            $item->expiresAfter(600); // Cache 1 heure
            try {
                $response = $this->httpClient->request('GET', self::RIOT_VERSIONS_URL);
                return $response->toArray();
            } catch (\Throwable $e) {
                $this->logger->error('Erreur lors de la récupération des versions Riot', [
                    'message' => $e->getMessage(),
                ]);
                return [];
            }
        });
    }
    /**
     * Récupère la liste des langues disponibles dans Data Dragon (API Riot Games)
     *
     * @return string[] Tableau des codes de langue au format "lang_REGION"
     *                  Exemple : ["fr_FR", "en_US", "ja_JP", ...]
     *
     * Mise en cache : 1 mois (2 592 000 secondes)
     */
    public function getLanguages(): array
    {
        return $this->cache->get('riot_languages', function (ItemInterface $item) {
            // Expiration dans 1 mois
            $item->expiresAfter(2592000);

            try {
                $response = $this->httpClient->request(
                    'GET',
                    'https://ddragon.leagueoflegends.com/cdn/languages.json'
                );
                return $response->toArray();
            } catch (\Throwable $e) {
                $this->logger->error('Erreur lors de la récupération des langues Riot', [
                    'message' => $e->getMessage(),
                ]);
                return [];
            }
        });
    }

    /**
     * Retourne un tableau de correspondance code_langue => nom affiché
     *
     * @return array<string, string>
     */
    public function getLanguageLabels(): array
    {
        return [
            'ar_AE' => 'Arabe (Émirats Arabes Unis)',
            'en_US' => 'Anglais (États-Unis)',
            'cs_CZ' => 'Tchèque',
            'de_DE' => 'Allemand',
            'el_GR' => 'Grec',
            'en_AU' => 'Anglais (Australie)',
            'en_GB' => 'Anglais (Royaume-Uni)',
            'en_PH' => 'Anglais (Philippines)',
            'en_SG' => 'Anglais (Singapour)',
            'es_AR' => 'Espagnol (Argentine)',
            'es_ES' => 'Espagnol (Espagne)',
            'es_MX' => 'Espagnol (Mexique)',
            'fr_FR' => 'Français',
            'hu_HU' => 'Hongrois',
            'id_ID' => 'Indonésien',
            'it_IT' => 'Italien',
            'ja_JP' => 'Japonais',
            'ko_KR' => 'Coréen',
            'pl_PL' => 'Polonais',
            'pt_BR' => 'Portugais (Brésil)',
            'ro_RO' => 'Roumain',
            'ru_RU' => 'Russe',
            'th_TH' => 'Thaï',
            'tr_TR' => 'Turc',
            'vi_VN' => 'Vietnamien',
            'zh_CN' => 'Chinois simplifié',
            'zh_MY' => 'Chinois (Malaisie)',
            'zh_TW' => 'Chinois traditionnel',
        ];
    }

    /**
     * Retourne true si la version existe dans la liste Riot.
     */
    public function versionExists(?string $version): bool
    {
        if (!is_string($version) || $version === '') {
            return false;
        }
        $versions = $this->getVersions();
        return in_array($version, $versions, true);
    }

    /**
     * Retourne true si la langue existe dans la liste Riot (fallback sur nos labels si l'API est KO).
     */
    public function languageExists(?string $language): bool
    {
        if (!is_string($language) || $language === '') {
            return false;
        }
        $languages = $this->getLanguages();
        if (empty($languages)) {
            $languages = array_keys($this->getLanguageLabels());
        }
        return in_array($language, $languages, true);
    }

    /**
     * Valide un couple (version, langue) et renvoie un petit rapport.
     *
     * @return array{ok:bool, errors:array<string,string>}
     */
    public function validateSelection(?string $version, ?string $language): array
    {
        $errors = [];

        if ($version !== null && $version !== '' && !$this->versionExists($version)) {
            $errors['version'] = 'Version inconnue : ' . $version;
        }

        if ($language !== null && $language !== '' && !$this->languageExists($language)) {
            $errors['language'] = 'Langue non supportée : ' . $language;
        }

        return [
            'ok'     => empty($errors),
            'errors' => $errors,
        ];
    }

}
