<?php
declare(strict_types=1);

namespace App\Service\Client;

use Symfony\Component\HttpFoundation\Cookie;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Owns the visitor's patch+language preference: where it is stored (session,
 * with the signed "remember" cookie as the durable backup) and what it falls
 * back to when the stored value no longer exists upstream.
 *
 * The cookie's wire format and signature belong to {@see RememberPreferencesCookie}.
 */
final class ClientManager
{
    private const K_LOCALE  = '_locale';
    private const K_VERSION = 'dd_version';

    private readonly RememberPreferencesCookie $rememberCookie;

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly VersionCatalogInterface $versionCatalog,
        // Injected from services.yaml (%kernel.secret%) — signs the remember cookie.
        #[\SensitiveParameter] string $appSecret = '',
        // Fallback used when no language could be detected.
        private readonly string $defaultLocale = 'en_US',
    ) {
        // Built here rather than wired: the codec is a pure function of the
        // secret this service is already bound to, so injecting it would only
        // duplicate the %kernel.secret% binding.
        $this->rememberCookie = new RememberPreferencesCookie($appSecret);
    }

    public function getLangue(): string
    {
        return $this->defaultLocale;
    }

    /** --- SET --- */

    /** @param string $locale e.g. "fr_FR" */
    public function setLocaleInSession(string $locale): void
    {
        $this->requestStack->getSession()?->set(self::K_LOCALE, $locale);
    }

    /** @param string $version e.g. "14.16.1" */
    public function setVersionInSession(string $version): void
    {
        $this->requestStack->getSession()?->set(self::K_VERSION, $version);
    }

    /**
     * Persistent cookie remembering the selection for $days days.
     *
     * @param string      $locale  locale to remember, e.g. "fr_FR"
     * @param string|null $version Data Dragon version to remember, e.g. "14.16.1"
     */
    public function makeRememberCookie(string $locale, ?string $version, int $days): Cookie
    {
        return $this->rememberCookie->create($locale, $version, $days);
    }

    /**
     * Selected DDragon locale (session first, then the "remember" cookie) without
     * starting or writing the session. It drives the UI locale on every request,
     * so it must stay free of session side effects.
     *
     * @return string|null e.g. "fr_FR", or null when nothing was remembered
     */
    public function getSelectedLocale(): ?string
    {
        $request = $this->requestStack->getCurrentRequest();
        if ($request === null) {
            return null;
        }

        // Session first, but only if one already exists (never start one here).
        if ($request->hasSession() && $request->hasPreviousSession()) {
            $locale = $request->getSession()->get(self::K_LOCALE);
            if (is_string($locale) && $locale !== '') {
                return $locale;
            }
        }

        $locale = $this->rememberCookie->read($request)['l'] ?? null;

        return (is_string($locale) && $locale !== '') ? $locale : null;
    }

    /**
     * Preferences from the session when they are complete (locale + version).
     * Otherwise tries to rehydrate the session from the "remember" cookie, then
     * returns whatever the session holds — possibly still partial.
     *
     * @return array{locale: ?string, version: ?string}
     */
    public function getOrHydratePreferences(): array
    {
        $preferences = $this->readPreferences();
        if ($preferences['locale'] !== null && $preferences['version'] !== null) {
            return $preferences;
        }

        // incomplete session → fall back to the cookie, then re-read
        $this->hydrateSessionFromRememberCookie();

        return $this->readPreferences();
    }

    /**
     * Version and language for the current visitor.
     *
     * Each axis falls back on its own: a patch that vanished between two visits
     * must not also reset a language the visitor picked (nor the other way
     * round), matching {@see PageContextResolver::selection()}. An unavailable
     * catalog yields '' for the version rather than an undefined index.
     *
     * @return array{version: string, lang: string}
     */
    public function getSession(): array
    {
        $preferences = $this->getOrHydratePreferences();

        return [
            'version' => $this->versionCatalog->versionExists($preferences['version'])
                ? (string) $preferences['version']
                : $this->versionCatalog->latestVersion(),
            'lang' => $this->versionCatalog->languageExists($preferences['locale'])
                ? (string) $preferences['locale']
                : $this->getLangue(),
        ];
    }

    /**
     * Hydrates the session from the "remember" cookie, but only when the session
     * does not already hold both the locale and the version — a live session
     * always wins over the cookie.
     *
     * The value is not validated functionally here ({@see getSession} does that
     * later).
     */
    private function hydrateSessionFromRememberCookie(): void
    {
        $request = $this->requestStack->getCurrentRequest();
        $session = $this->requestStack->getSession();
        if (!$request || !$session) {
            return;
        }

        if ($session->has(self::K_LOCALE) && $session->has(self::K_VERSION)) {
            return; // already hydrated
        }

        $payload = $this->rememberCookie->read($request);
        if ($payload === null) {
            return;
        }

        if (!empty($payload['l'])) {
            $session->set(self::K_LOCALE, (string) $payload['l']);
        }
        if (!empty($payload['v'])) {
            $session->set(self::K_VERSION, (string) $payload['v']);
        }
    }

    /**
     * Session preferences, empty strings normalised to null.
     *
     * @return array{locale: ?string, version: ?string}
     */
    private function readPreferences(): array
    {
        $session = $this->requestStack->getSession();
        $locale  = $session?->get(self::K_LOCALE);
        $version = $session?->get(self::K_VERSION);

        return [
            'locale'  => (is_string($locale) && $locale !== '') ? $locale : null,
            'version' => (is_string($version) && $version !== '') ? $version : null,
        ];
    }
}
