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
 * single request and each list/detail URL is a pure function of its query —
 * which is what makes those responses safe to HTTP-cache (see the cache layer).
 */
final class PageContextResolver
{
    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly ClientManager $clientManager,
        private readonly VersionManager $versionManager,
    ) {}

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
