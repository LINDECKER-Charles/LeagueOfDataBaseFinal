<?php
declare(strict_types=1);

namespace App\Service\Client;

use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Resolves the (version, lang) a list/detail page renders with, from the request
 * query — falling back to the session-remembered selection
 * ({@see ClientManager::getSession()}) when the query omits or invalidates them.
 * It never redirects.
 *
 * This replaces the old "*_redirect" bounce, which existed only to copy the
 * session-remembered selection into the URL via a 302 before the real page
 * could render. Links now carry version+lang directly, so every navigation is a
 * single request and each list/detail URL is a pure function of its query.
 *
 * The URL is a pure function of its query, but the RENDER is not: the UI locale
 * is resolved from the session at kernel.request ({@see \App\EventSubscriber\LocaleSubscriber}),
 * so Symfony marks these responses Cache-Control: private. There is deliberately
 * no shared HTTP-cache layer today — enabling one would mean carrying the locale
 * in the URL rather than the session, then adding s-maxage + ETag.
 */
final class PageContextResolver
{
    /** Home preview count per resource (mirrors {@see \App\Controller\HomeController::home()}). */
    public const HOME_PER_PAGE = 4;

    /**
     * Images the streaming loader pre-warms for a list page = the first client
     * page the visitor sees on arrival. List pages render the whole set in one
     * pass and the ResourceFilter island paginates it client-side (mirrors the
     * `pageSize` default in components/list_filter.html.twig); warming that first
     * visible page keeps every above-the-fold card off a placeholder, while the
     * rest lazy-load and warm through the deferred ingestor as the user scrolls
     * or paginates. Full-warming the whole set would only swap this cheap gate
     * for a multi-second one.
     */
    public const LIST_INITIAL_PAGE_SIZE = 12;

    /** Resource types the home page previews, in display order. */
    private const HOME_TYPES = ['champion', 'item', 'summoner', 'runesReforged'];

    /**
     * List route (path) => the resource type it renders, the images the loader
     * pre-warms ({@see self::LIST_INITIAL_PAGE_SIZE}) and the cap on a
     * caller-supplied page size. Read only by the streaming loader
     * ({@see loaderSteps}); drift only under-/over-warms a few images — the
     * deferred ingestor and the next visit reconcile it — so it never breaks a render.
     */
    public const LIST_PAGES = [
        '/champions' => ['type' => 'champion',      'defaultPerPage' => self::LIST_INITIAL_PAGE_SIZE, 'maxPerPage' => 20],
        '/objects'   => ['type' => 'item',          'defaultPerPage' => self::LIST_INITIAL_PAGE_SIZE, 'maxPerPage' => 20],
        '/runes'     => ['type' => 'runesReforged', 'defaultPerPage' => self::LIST_INITIAL_PAGE_SIZE, 'maxPerPage' => 20],
        '/summoners' => ['type' => 'summoner',      'defaultPerPage' => self::LIST_INITIAL_PAGE_SIZE, 'maxPerPage' => 0],
    ];

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly ClientManager $clientManager,
        private readonly VersionManager $versionManager,
    ) {}

    /**
     * Resource-warming steps for a destination path. Pagination is taken from the
     * explicit $page/$perPage arguments (the caller passes the SSE query), never
     * from the ambient request or the session — so the loader stream holds no
     * session lock and this method has no hidden RequestStack dependency. null
     * means "absent from the query" → the route default applies. Empty for pages
     * that ingest no image batch (detail, working-progress).
     *
     * @param int|null $page    requested page number (null → 1)
     * @param int|null $perPage requested page size (null/<=0 → route default; clamped to route max)
     * @return list<array{type:string, perPage:int, page:int}>
     */
    public function loaderSteps(string $path, ?int $page = null, ?int $perPage = null): array
    {
        // A versioned list path (/{version}/champions) warms the same image batch
        // as its clean form — drop the leading version segment before matching.
        $path = preg_replace('#^/' . VersionManager::VERSION_PATTERN . '(?=/)#', '', $path) ?? $path;
        $p = strtolower(rtrim($path, '/')) ?: '/';

        if ($p === '/') {
            return array_map(
                static fn (string $type): array => ['type' => $type, 'perPage' => self::HOME_PER_PAGE, 'page' => 1],
                self::HOME_TYPES,
            );
        }

        $cfg = self::LIST_PAGES[$p] ?? null;
        if ($cfg === null) {
            return [];
        }

        $page    = max(1, $page ?? 1);
        $perPage = $perPage ?? 0;
        if ($perPage <= 0) {
            $perPage = $cfg['defaultPerPage'];
        }
        if ($cfg['maxPerPage'] > 0) {
            $perPage = min($perPage, $cfg['maxPerPage']);
        }

        return [['type' => $cfg['type'], 'perPage' => $perPage, 'page' => $page]];
    }

    /**
     * Version + language for the current request. Version and language resolve
     * independently so a `/{version}/…` path (or a bare `?version=`) selects the
     * patch while the language still falls back to the session — a versioned URL
     * is language-invariant, its content lang is the visitor's own.
     *
     * Version precedence: route path segment (`/{version}/…`) > `?version=` query
     * > session. Language precedence: `?lang=` query > session. Never redirects.
     *
     * @return array{version:string, lang:string}
     */
    public function selection(): array
    {
        $request = $this->requestStack->getCurrentRequest();

        $version = $this->requestVersion($request);
        $lang    = $this->requestLang($request);

        if ($version !== '' && $lang !== '') {
            return ['version' => $version, 'lang' => $lang];
        }

        // Session already defaults to latest version + default locale when unset;
        // only touched when the request under-specifies (keeps shareable URLs
        // session-free).
        $session = $this->clientManager->getSession();

        return [
            'version' => $version !== '' ? $version : $session['version'],
            'lang'    => $lang !== '' ? $lang : $session['lang'],
        ];
    }

    /** Valid version from the route path segment, else the query — '' when neither applies. */
    private function requestVersion(?Request $request): string
    {
        foreach ([$request?->attributes->get('version'), $request?->query->get('version')] as $candidate) {
            $candidate = trim((string) ($candidate ?? ''));
            if ($candidate !== '' && $this->versionManager->versionExists($candidate)) {
                return $candidate;
            }
        }

        return '';
    }

    /** Valid language from the query — '' when absent or unknown. */
    private function requestLang(?Request $request): string
    {
        $lang = trim((string) ($request?->query->get('lang') ?? ''));

        return $lang !== '' && $this->versionManager->languageExists($lang) ? $lang : '';
    }
}
