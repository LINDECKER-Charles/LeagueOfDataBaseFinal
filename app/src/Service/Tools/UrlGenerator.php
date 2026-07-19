<?php
declare(strict_types=1);

namespace App\Service\Tools;

use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

final class UrlGenerator
{
    /**
     * Paths on which a version/lang change must not rewrite the URL — there is no
     * resource to re-render (the selection only lands in the session/cookie).
     */
    private const SELECTION_INERT_PATHS = ['/working-progress'];

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly UrlGeneratorInterface $router,
    ) {}

    /**
     * Back-URL from the HTTP "referer", falling back to a route when it is absent
     * or (optionally) cross-host — the guard against an open redirect.
     */
    public function generateBackurl(
        string $fallbackRoute = 'app_home',
        array $fallbackParams = [],
        bool $sameHostOnly = true,
    ): string {
        $request = $this->requestStack->getCurrentRequest();

        $fallback = $this->router->generate($fallbackRoute, $fallbackParams);
        if (!$request) {
            return $fallback;
        }

        $referer = (string) ($request->headers->get('referer') ?? '');
        if ($referer === '') {
            return $fallback;
        }

        if ($sameHostOnly) {
            $host = $request->getSchemeAndHttpHost();
            if ($host === '' || !str_starts_with($referer, $host)) {
                return $fallback;
            }
        }

        return $referer;
    }

    /**
     * Rewrite a back-URL so it renders under a new (version, lang) selection.
     *
     * The version's home depends on the URL shape and MUST match
     * {@see \App\Service\Client\PageContextResolver} precedence
     * (path segment > ?version= > session):
     *  - versioned path (`/{version}/champion/…`) → swap the leading segment, so the
     *    new version actually wins. Writing it only to `?version=` would leave the
     *    old path segment in place and shadow it — the exact bug this handles.
     *  - any other path → the version rides the query (`?version=`).
     * The language is never a path segment, so it always rides the query (`?lang=`).
     * Existing unrelated query params and the fragment are preserved.
     */
    public function applySelection(string $url, string $version, string $lang): string
    {
        $path     = parse_url($url, PHP_URL_PATH) ?: '/';
        $query    = (string) (parse_url($url, PHP_URL_QUERY) ?: '');
        $fragment = parse_url($url, PHP_URL_FRAGMENT);

        if (in_array($path, self::SELECTION_INERT_PATHS, true)) {
            return $url;
        }

        parse_str($query, $params);
        unset($params['version'], $params['lang']); // re-derived below

        $versionedSegment = '#^/'.VersionManager::VERSION_PATTERN.'(?=/)#';
        if (preg_match($versionedSegment, $path) === 1) {
            $path = (string) preg_replace($versionedSegment, '/'.$version, $path, 1);
        } else {
            $params['version'] = $version; // no path segment to own it
        }
        $params['lang'] = $lang;

        $newUrl = $path;
        if ($params !== []) {
            $newUrl .= '?'.http_build_query($params);
        }
        if (is_string($fragment) && $fragment !== '') {
            $newUrl .= '#'.$fragment;
        }

        return $newUrl;
    }
}
