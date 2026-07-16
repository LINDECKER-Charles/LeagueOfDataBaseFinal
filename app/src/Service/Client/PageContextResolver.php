<?php
declare(strict_types=1);

namespace App\Service\Client;

use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Resolves the (version, lang[, page, perPage]) a list/detail page renders with,
 * from the request query — falling back to the session-remembered selection
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

    /** Resource types the home page previews, in display order. */
    private const HOME_TYPES = ['champion', 'item', 'summoner', 'runesReforged'];

    /**
     * List route (path) => the resource type it renders and its pagination
     * defaults. Kept in sync with each list controller's listContext() call;
     * the streaming loader reads it to warm exactly what the destination shows.
     * Drift only under-/over-warms a few images — the deferred ingestor and the
     * next visit reconcile it — so it never breaks a render.
     */
    public const LIST_PAGES = [
        '/champions' => ['type' => 'champion',      'defaultPerPage' => 20, 'maxPerPage' => 20],
        '/objects'   => ['type' => 'item',          'defaultPerPage' => 8,  'maxPerPage' => 20],
        '/runes'     => ['type' => 'runesReforged', 'defaultPerPage' => 8,  'maxPerPage' => 20],
        '/summoners' => ['type' => 'summoner',      'defaultPerPage' => 8,  'maxPerPage' => 0],
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
     * that ingest no image batch (detail, setup, working-progress).
     *
     * @param int|null $page    requested page number (null → 1)
     * @param int|null $perPage requested page size (null/<=0 → route default; clamped to route max)
     * @return list<array{type:string, perPage:int, page:int}>
     */
    public function loaderSteps(string $path, ?int $page = null, ?int $perPage = null): array
    {
        $p = strtolower(rtrim($path, '/')) ?: '/';

        if ($p === '/home') {
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
     * Version + language for the current request: valid query params win
     * (shareable/cacheable URLs), otherwise the session selection.
     *
     * @return array{version:string, lang:string}
     */
    public function selection(): array
    {
        $query   = $this->requestStack->getCurrentRequest()?->query;
        $version = trim((string) ($query?->get('version') ?? ''));
        $lang    = trim((string) ($query?->get('lang') ?? ''));

        if ($version !== '' && $lang !== ''
            && $this->versionManager->versionExists($version)
            && $this->versionManager->languageExists($lang)
        ) {
            return ['version' => $version, 'lang' => $lang];
        }

        // Already defaulted to latest version + default locale when unset.
        return $this->clientManager->getSession();
    }

    /**
     * Selection plus pagination, both taken from the query with safe defaults.
     *
     * @param int $maxPerPage 0 = unlimited (summoner spells show the whole set)
     * @return array{version:string, lang:string, numPage:int, itemPerPage:int}
     */
    public function listContext(int $defaultPerPage = 8, int $maxPerPage = 20): array
    {
        $query   = $this->requestStack->getCurrentRequest()?->query;
        $numPage = max(1, (int) ($query?->get('numpage') ?? 1));

        $perPage = (int) ($query?->get('itemperpage') ?? $defaultPerPage);
        if ($perPage <= 0) {
            $perPage = $defaultPerPage;
        }
        if ($maxPerPage > 0) {
            $perPage = min($perPage, $maxPerPage);
        }

        return $this->selection() + ['numPage' => $numPage, 'itemPerPage' => $perPage];
    }
}
