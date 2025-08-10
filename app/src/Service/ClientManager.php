<?php
declare(strict_types=1);

namespace App\Service;

use App\Service\VersionManager;
use Symfony\Component\HttpFoundation\Cookie;
use Symfony\Component\HttpFoundation\RequestStack;

final class ClientManager
{
    private const K_LOCALE  = '_locale';
    private const K_VERSION = 'dd_version';
    private const REMEMBER_NAME  = 'lod_prefs'; // nom du cookie

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly string $appSecret = '', // injecte %kernel.secret% via services.yaml si tu veux signer
        private readonly string $defaultLocale = 'en_US', // fallback si pas d'entête
        private readonly VersionManager $versionManager,
    ) {}

    /**
     * Retourne la langue préférée du client telle qu'envoyée dans l'entête Accept-Language.
     * - Exemple header: "fr-CA, fr;q=0.9, en-US;q=0.8"
     * - Renverra: "fr_CA"
     * - Si aucune langue n'est fournie: $defaultLocale
     *
     * ⚠️ Ici on ne vérifie PAS si la langue est supportée par votre app/RIOT.
     */
    public function getLangue(): string
    {
        $request = $this->requestStack->getCurrentRequest();
        if (!$request) {
            return $this->defaultLocale; // contexte CLI/worker
        }

        // Langues triées par priorité (q)
        $langs = $request->getLanguages(); // ex: ['fr-CA', 'fr', 'en-US', 'en']
        $first = $langs[0] ?? null;

        if (!$first) {
            return $this->defaultLocale;
        }

        return $this->normalizeTag($first);
    }

    /**
     * A Spliter dans un service d'edition TXT
     * Normalise un tag BCP47 en format "xx_YY"
     * - 'fr'      -> 'fr'
     * - 'fr-FR'   -> 'fr_FR'
     * - 'EN-us'   -> 'en_US'
     */
    private function normalizeTag(string $tag): string
    {
        $tag = str_replace('-', '_', $tag);

        if (str_contains($tag, '_')) {
            [$lang, $region] = explode('_', $tag, 2);
            return strtolower($lang) . '_' . strtoupper($region);
        }

        return strtolower($tag);
    }

    /** --- SET --- */

    /**
     * Enregistre la langue en session.
     *
     * @param string $locale Exemple: "fr_FR"
     */
    public function setLocaleInSession(string $locale): void
    {
        $this->requestStack->getSession()?->set(self::K_LOCALE, $locale);
    }

    /**
     * Enregistre la version Data Dragon en session.
     *
     * @param string $version Exemple: "14.16.1"
     */
    public function setVersionInSession(string $version): void
    {
        $this->requestStack->getSession()?->set(self::K_VERSION, $version);
    }

    /**
     * Enregistre en une fois la langue et la version en session.
     * Les valeurs nulles ou vides sont ignorées.
     *
     * @param string|null $locale  Exemple: "fr_FR"
     * @param string|null $version Exemple: "14.16.1"
     */
    public function setPreferencesInSession(?string $locale, ?string $version): void
    {
        $session = $this->requestStack->getSession();
        if (!$session) {
            return;
        }

        if (is_string($locale) && $locale !== '') {
            $session->set(self::K_LOCALE, $locale);
        }
        if (is_string($version) && $version !== '') {
            $session->set(self::K_VERSION, $version);
        }
    }

    /** --- GET --- */

    /**
     * Récupère la langue depuis la session.
     *
     * @return string|null "fr_FR" par ex., ou null si absente/vides
     */
    public function getLocaleFromSession(): ?string
    {
        $v = $this->requestStack->getSession()?->get(self::K_LOCALE);
        return (is_string($v) && $v !== '') ? $v : null;
    }

    /**
     * Récupère la version Data Dragon depuis la session.
     *
     * @return string|null "14.16.1" par ex., ou null si absente/vides
     */
    public function getVersionFromSession(): ?string
    {
        $v = $this->requestStack->getSession()?->get(self::K_VERSION);
        return (is_string($v) && $v !== '') ? $v : null;
    }

    /**
     * Récupère langue + version d'un coup.
     *
     * @return array{locale: ?string, version: ?string}
     */
    public function getPreferencesFromSession(): array
    {
        return [
            'locale'  => $this->getLocaleFromSession(),
            'version' => $this->getVersionFromSession(),
        ];
    }

    
    /**
     * Crée un cookie "remember" signé qui persiste la locale (et optionnellement la version)
     * pendant $days jours. Le JSON est base64-encodé puis concaténé avec une signature HMAC
     * (sha256) basée sur le secret applicatif : base64(json) . '|' . hmac.
     *
     * Sécurité : le cookie garantit l'intégrité (signature) mais pas la confidentialité
     * (pas chiffré). Ne pas y stocker de données sensibles.
     *
     * @param string      $locale  Locale à mémoriser (ex. "fr_FR")
     * @param string|null $version Version Data Dragon à mémoriser (ex. "14.16.1") ou null
     * @param int         $days    Durée de vie en jours (par défaut 7)
     *
     * @return \Symfony\Component\HttpFoundation\Cookie Cookie persistant (Secure, HttpOnly, SameSite=Lax)
     */
    public function makeRememberCookie(string $locale, ?string $version, int $days = 7): Cookie
    {
        $payload = ['l' => $locale, 'v' => $version];
        $json    = json_encode($payload, JSON_UNESCAPED_SLASHES);
        $sig     = hash_hmac('sha256', $json, $this->appSecret ?: 'fallback-secret');
        $value   = base64_encode($json).'|'.$sig;

        $expire = time() + ($days * 86400);

        return Cookie::create(
            name: self::REMEMBER_NAME,
            value: $value,
            expire: $expire,
            path: '/',
            secure: true,
            httpOnly: true,
            sameSite: 'lax'
        );
    }

    /**
     * Crée un cookie d'effacement pour le cookie "remember".
     *
     * Le cookie renvoyé a la même clé que le cookie de persistance mais une valeur vide
     * et une date d'expiration dans le passé, ce qui indique au navigateur de le supprimer.
     *
     * Important :
     * - Les attributs (path/secure/httponly/samesite, et éventuellement domain) doivent
     *   correspondre à ceux utilisés lors de la création du cookie pour garantir l'effacement.
     * - Cette opération ne touche pas aux données serveur (session, base, etc.) : elle ne supprime
     *   que le cookie côté client.
     *
     * @return \Symfony\Component\HttpFoundation\Cookie Cookie “deletion” (expire immédiatement)
     */
    public function makeForgetCookie(): Cookie
    {
        return Cookie::create(
            name: self::REMEMBER_NAME,
            value: '',
            expire: time() - 3600,
            path: '/',
            secure: true,
            httpOnly: true,
            sameSite: 'lax'
        );
    }

    /**
     * Hydrate la session à partir du cookie "remember" si la session ne contient pas déjà
     * la langue et la version. Le cookie doit être au format:
     *   base64(json) . '|' . hmac_sha256(json, appSecret)
     *
     * Étapes:
     * - Ne fait rien si la session a déjà _locale ET dd_version.
     * - Lit le cookie (nom = self::REMEMBER_NAME). S’il est absent ou mal formé, sort.
     * - Décode la partie base64 en JSON, recalcule la signature HMAC-SHA256 avec le secret
     *   applicatif et vérifie l’intégrité via hash_equals. En cas d’échec, ignore.
     * - Décode le JSON en tableau et, si présents, écrit:
     *     - _locale  <- $data['l']
     *     - dd_version <- $data['v']
     *
     * Remarques:
     * - Garantit l’intégrité (HMAC) mais pas la confidentialité (contenu en clair).
     * - Ne valide pas la valeur fonctionnellement (à faire via VersionManager si nécessaire).
     * - À appeler tôt dans le cycle requête (ex: subscriber sur KernelEvents::REQUEST).
     *
     * @return void
     */
    public function hydrateSessionFromRememberCookie(): void
    {
        $req = $this->requestStack->getCurrentRequest();
        $sess = $this->requestStack->getSession();
        if (!$req || !$sess) return;

        if ($sess->has(self::K_LOCALE) && $sess->has(self::K_VERSION)) {
            return; // déjà hydraté
        }

        $raw = $req->cookies->get(self::REMEMBER_NAME);
        if (!$raw || !str_contains($raw, '|')) return;

        [$b64, $sig] = explode('|', $raw, 2);
        $json = base64_decode($b64, true);
        if ($json === false) return;

        $expected = hash_hmac('sha256', $json, $this->appSecret ?: 'fallback-secret');
        if (!hash_equals($expected, $sig)) return; // cookie altéré → on ignore

        $data = json_decode($json, true);
        if (!is_array($data)) return;

        if (!empty($data['l'])) $sess->set(self::K_LOCALE, (string)$data['l']);
        if (!empty($data['v'])) $sess->set(self::K_VERSION, (string)$data['v']);
    }

    /**
     * Retourne les préférences depuis la session si elles sont complètes (locale + version).
     * Sinon, tente de réhydrater la session depuis le cookie "remember", puis renvoie
     * ce que contient la session (même partiel).
     *
     * @return array{locale: ?string, version: ?string}
     */
    public function getOrHydratePreferences(): array
    {
        $sess = $this->requestStack->getSession();

        if (!$sess) {
            return ['locale' => null, 'version' => null];
        }

        $loc = $sess->get(self::K_LOCALE);
        $ver = $sess->get(self::K_VERSION);
        $hasLoc = is_string($loc) && $loc !== '';
        $hasVer = is_string($ver) && $ver !== '';

        if ($hasLoc && $hasVer) {
            return ['locale' => $loc, 'version' => $ver];
        }

        // session incomplète → on essaie le cookie
        $this->hydrateSessionFromRememberCookie();

        // relire après hydratation
        $loc = $sess->get(self::K_LOCALE);
        $ver = $sess->get(self::K_VERSION);

        return [
            'locale'  => (is_string($loc) && $loc !== '') ? $loc : null,
            'version' => (is_string($ver) && $ver !== '') ? $ver : null,
        ];
    }

    public function getSession(): array{
        $val = $this->getOrHydratePreferences();
        $lang    = $val['locale']  ?? $this->getLangue(); 
        $version = $val['version'] ?? $this->versionManager->getVersions()[0];
        return ['version' => $version, 'lang' => $lang];     
    }
}
