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
        private readonly LoggerInterface $logger,
        private readonly APICaller $aPICaller,
    ) {}

    /* Partie API */

    /**
     * Récupère la liste complète des versions depuis Riot (Data Dragon)
     *
     * @return array Liste des versions, la première étant la plus récente
     */
    public function getVersions(): array
    {
        //$this->cache->delete('riot_versions'); //pour les tests d'optimisation
        return $this->cache->get('riot_versions', function (ItemInterface $item) {
            $item->expiresAfter(600); //Expire dans 10min
            try {
                $response = $this->httpClient->request('GET', self::RIOT_VERSIONS_URL);
                $versions = array_values(array_filter(
                    $response->toArray(),
                    fn($v) => !(is_string($v) && preg_match('/^lol/', $v))
                ));
                return $versions;
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
            'ar_AE' => 'Arabic (United Arab Emirates)',
            'en_US' => 'English (United States)',
            'cs_CZ' => 'Czech',
            'de_DE' => 'German',
            'el_GR' => 'Greek',
            'en_AU' => 'English (Australia)',
            'en_GB' => 'English (United Kingdom)',
            'en_PH' => 'English (Philippines)',
            'en_SG' => 'English (Singapore)',
            'es_AR' => 'Spanish (Argentina)',
            'es_ES' => 'Spanish (Spain)',
            'es_MX' => 'Spanish (Mexico)',
            'fr_FR' => 'French',
            'hu_HU' => 'Hungarian',
            'id_ID' => 'Indonesian',
            'it_IT' => 'Italian',
            'ja_JP' => 'Japanese',
            'ko_KR' => 'Korean',
            'pl_PL' => 'Polish',
            'pt_BR' => 'Portuguese (Brazil)',
            'ro_RO' => 'Romanian',
            'ru_RU' => 'Russian',
            'th_TH' => 'Thai',
            'tr_TR' => 'Turkish',
            'vi_VN' => 'Vietnamese',
            'zh_CN' => 'Chinese (Simplified)',
            'zh_MY' => 'Chinese (Malaysia)',
            'zh_TW' => 'Chinese (Traditional)',
        ];
    }

    /* Vérification */

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
