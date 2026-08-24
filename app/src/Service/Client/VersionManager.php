<?php
declare(strict_types=1);

namespace App\Service\Client;

use App\Service\Tools\GoFetcherClient;
use Monolog\Attribute\WithMonologChannel;
use Psr\Log\LoggerInterface;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

#[WithMonologChannel('catalog')]
final class VersionManager implements VersionCatalogInterface
{
    /**
     * Shape of a Data Dragon version (dotted numeric, e.g. "15.14.1", "1.0.0.152").
     * Single source for the `/{version}/…` route requirement and the loader's
     * path-prefix strip — a clean segment ("champion", "objects") never matches.
     */
    public const VERSION_PATTERN = '\d+(?:\.\d+)+';

    /**
     * Leading `/{version}` path segment, anchored on a full segment. The loader
     * strips it and the selection rewriter replaces it: both MUST recognise the
     * exact same shape, so the whole expression — not just the number pattern —
     * lives here.
     */
    public const VERSION_SEGMENT_REGEX = '#^/' . self::VERSION_PATTERN . '(?=/)#';

    /**
     * Data Dragon still publishes pre-2013 `lolpatch_*` entries in its version
     * list; they carry no modern dataset, so they never reach a caller.
     */
    private const LEGACY_VERSION_PREFIX = 'lol';

    private const VERSIONS_CACHE_KEY = 'riot_versions';

    /**
     * Patches ship roughly biweekly, so a shorter TTL only multiplied the Data
     * Dragon round trips with no freshness benefit.
     */
    private const VERSIONS_TTL_S = 3600;

    private const LANGUAGES_CACHE_KEY = 'riot_languages';

    /** The Data Dragon locale list barely ever moves. */
    private const LANGUAGES_TTL_S = 2592000;

    /**
     * Data Dragon locale => English display name. Also the offline fallback of
     * {@see languageExists()} when the upstream language list is unreachable.
     */
    private const LANGUAGE_LABELS = [
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

    /** @var string[]|null in-request memo (getVersions is called several times per request) */
    private ?array $versionsMemo = null;

    /** @var string[]|null in-request memo */
    private ?array $languagesMemo = null;

    public function __construct(
        private readonly GoFetcherClient $goFetcher,
        private readonly CacheInterface $cache,
        private readonly LoggerInterface $logger,
    ) {}

    /* API */

    /**
     * @return list<string> versions, the first one being the most recent
     */
    public function getVersions(): array
    {
        // Called 3+ times per request (param validation, ClientData, redirects).
        // Memoize in-request so the cross-request pool is hit at most once.
        return $this->versionsMemo ??= $this->cache->get(
            self::VERSIONS_CACHE_KEY,
            function (ItemInterface $item) {
                $item->expiresAfter(self::VERSIONS_TTL_S);
                try {
                    return array_values(array_filter(
                        $this->goFetcher->versions(),
                        static fn (mixed $version): bool => is_string($version)
                            && !str_starts_with($version, self::LEGACY_VERSION_PREFIX),
                    ));
                } catch (\Throwable $e) {
                    $this->logger->error('catalog.versions.unavailable', ['exception' => $e]);

                    return [];
                }
            }
        );
    }

    /**
     * Newest known patch, or '' when the version list is unavailable.
     *
     * Single answer to "which patch is current?": every caller (URL builder,
     * sitemap, session fallback) used to re-implement `getVersions()[0]` with its
     * own guard, and the unguarded ones broke on an upstream outage.
     */
    public function latestVersion(): string
    {
        try {
            return (string) ($this->getVersions()[0] ?? '');
        } catch (\Throwable) {
            // Crawler- and render-facing callers must degrade, never 500: an
            // unreachable cache backend is not a reason to lose a whole page.
            return '';
        }
    }

    /**
     * @return string[] language codes in "lang_REGION" form, e.g. ["fr_FR", "en_US", "ja_JP", ...]
     */
    public function getLanguages(): array
    {
        return $this->languagesMemo ??= $this->cache->get(
            self::LANGUAGES_CACHE_KEY,
            function (ItemInterface $item) {
                $item->expiresAfter(self::LANGUAGES_TTL_S);
                try {
                    return $this->goFetcher->languages();
                } catch (\Throwable $e) {
                    $this->logger->error('catalog.languages.unavailable', ['exception' => $e]);

                    return [];
                }
            }
        );
    }

    /**
     * @return array<string, string> language code => display name
     */
    public function getLanguageLabels(): array
    {
        return self::LANGUAGE_LABELS;
    }

    /* Validation */

    public function versionExists(?string $version): bool
    {
        if (!is_string($version) || $version === '') {
            return false;
        }
        $versions = $this->getVersions();
        return in_array($version, $versions, true);
    }

    /** Falls back to our own label list when the Riot API is down. */
    public function languageExists(?string $language): bool
    {
        if (!is_string($language) || $language === '') {
            return false;
        }
        $languages = $this->getLanguages();
        if (empty($languages)) {
            $languages = array_keys(self::LANGUAGE_LABELS);
        }
        return in_array($language, $languages, true);
    }

    /**
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
