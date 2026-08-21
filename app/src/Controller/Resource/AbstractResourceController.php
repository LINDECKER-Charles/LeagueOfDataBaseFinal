<?php
declare(strict_types=1);

namespace App\Controller\Resource;

use App\Controller\AbstractPageController;
use App\Service\API\AbstractManager;
use App\Service\API\CategoriesInterface;
use App\Service\API\DatasetRef;
use App\Service\API\Edition\EditionAwareInterface;
use App\Service\API\ResourceNotFoundException;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Response;

/**
 * Base of the Data Dragon resource controllers (champion / item / rune /
 * summoner) — and of them only. On top of the transverse page plumbing
 * ({@see AbstractPageController}) it owns what is specific to browsing a DDragon
 * collection: the versioned route requirement, the list render, the search
 * endpoint and the detail-page failure policy.
 */
abstract class AbstractResourceController extends AbstractPageController
{
    /**
     * Route requirement for the `/{version}/...` resource routes so a clean first
     * segment (`champion`, `objects`, …) never collides with the versioned form.
     */
    public const VERSION_ROUTE_REQUIREMENT = VersionManager::VERSION_PATTERN;

    /** Result cap of the name-search endpoints (see {@see searchResponse}). */
    protected const SEARCH_RESULT_LIMIT = 20;

    /**
     * List pages render the whole collection in one pass — the ResourceFilter
     * island owns search, facets and pagination client-side. Mirrors the
     * "0 = no slice (up to the per-page cap)" contract of
     * {@see \App\Service\API\Concern\PaginatesResources::paginate()}.
     */
    private const ALL_ENTRIES = 0;

    /**
     * Full-collection list render, identical for the four resources: resolve the
     * selection, paginate, echo it in `meta`, render.
     *
     * @param callable(array<string,mixed>, string, string): array<string,mixed>|null $extraVars
     *        template variables derived from the paginated data (e.g. the items'
     *        upgrade index), evaluated after a successful fetch
     */
    protected function listPage(
        AbstractManager $manager,
        string $template,
        ?callable $extraVars = null,
    ): Response {
        $selection = $this->pageContext->selection();
        ['version' => $version, 'lang' => $lang] = $selection;

        try {
            $data = $manager->paginate(DatasetRef::fromSelection($selection), self::ALL_ENTRIES);
        } catch (\Throwable $e) {
            return $this->redirectToHomeWithError(['version' => $version, 'lang' => $lang], $e);
        }

        $data['meta']['version'] = $version;
        $data['meta']['lang']    = $lang;
        $collection = $manager->type() . 's';

        return $this->render($template, [
            $collection => $data[$collection],
            'images'    => $data['images'],
            'meta'      => $data['meta'],
            'client'    => $this->clientData(),
        ] + ($extraVars === null ? [] : $extraVars($data, $version, $lang)));
    }

    /**
     * Name-search endpoint of a resource → `[{id, name, image}]`, plus `edition`
     * for the resources that exist in both games (item, summoner — see
     * {@see \App\Service\API\Edition\Edition}): a "Flash" hit list is ambiguous
     * without it.
     *
     * The image association is the caller's business because the managers do not
     * agree on the shape of `getImages()` (positional list for champions, map
     * keyed by id for items and summoners).
     *
     * @param callable(list<array<string,mixed>>, array<mixed>): array<int,?string> $imagesFor
     *        image URLs aligned on the matched rows
     */
    protected function searchResponse(
        CategoriesInterface $manager,
        string $name,
        callable $imagesFor,
    ): JsonResponse {
        $dataset = DatasetRef::fromSelection($this->pageContext->selection());

        try {
            $rows = $manager->searchByName($name, $dataset, self::SEARCH_RESULT_LIMIT);
        } catch (\Throwable) {
            return $this->searchUnavailable($dataset);
        }

        if ($rows === []) {
            return $this->json([]);
        }

        return $this->json(array_map(
            static fn (array $row, ?string $image): array => [
                'id'    => $row['id'] ?? null,
                'name'  => $row['name'] ?? '',
                'image' => $image,
            ] + array_intersect_key($row, ['edition' => true]),
            $rows,
            $imagesFor($rows, $manager->getImages($dataset, false, $rows)),
        ));
    }

    /**
     * Data-layer outage answered to a machine caller: a real failure STATUS and a
     * neutral body. The former 200-with-a-french-string made success and failure
     * indistinguishable and leaked the exception (mirrors
     * {@see \App\Controller\Api\PickerController}).
     */
    private function searchUnavailable(DatasetRef $dataset): JsonResponse
    {
        return new JsonResponse(
            ['error' => sprintf(
                'Resource data unavailable for version %s / language %s.',
                $dataset->version,
                $dataset->lang
            )],
            Response::HTTP_SERVICE_UNAVAILABLE,
        );
    }

    /**
     * Redirects to the home page, signalling the missing data (invalid
     * version/lang or upstream outage) through an error flash — the header
     * switcher there lets the visitor fix the selection.
     *
     * @param array{version?:string, lang?:string} $ctx
     */
    protected function redirectToHomeWithError(array $ctx, \Throwable $e): Response
    {
        $this->requestStack->getSession()->getFlashBag()->clear();
        $this->addFlash('error', $this->dataError($ctx, $e));

        return $this->redirectToRoute('app_home');
    }

    /**
     * Maps a detail-page data failure to its HTTP outcome.
     *
     * A slug unknown to the requested dataset is a definitive absence. On the
     * canonical clean URL (`/object/{name}`, patch resolved from the session) that
     * stays a real 404 — crawlers must get an honest 404 for a bad slug, never a
     * soft redirect. But when the URL itself pins a historical patch the slug may
     * simply not exist in it yet (an entity added later): switching version on a
     * detail page lands here. Rather than a dead-end, send the visitor to that
     * patch's list — a real, related page, reached by a plain 302 the crawler reads
     * as a redirect, not a soft-404.
     *
     * "Pinned in the URL" covers BOTH forms the version can take (mirroring
     * {@see \App\Service\Client\PageContextResolver}: path > query): the
     * `/{version}/object/{name}` path segment (JS switcher / canonical historical
     * link) and a `?version=` query (no-JS switcher fallback, shared links). A patch
     * defaulted from the session is NOT pinned — the canonical URL keeps 404-ing.
     *
     * Anything else (upstream 5xx, timeout, corrupt payload) keeps the historical
     * redirect-with-flash so the visitor can fix the version/lang selection.
     *
     * @param array{version?:string, lang?:string} $ctx
     * @param string $versionedListRoute versioned list route of this resource
     *                                   (e.g. "app_items_versioned")
     */
    protected function detailFailure(
        array $ctx,
        \Throwable $e,
        string $versionedListRoute
    ): Response {
        if ($e instanceof ResourceNotFoundException) {
            $version = $this->urlPinnedVersion();
            if ($version !== null) {
                $params = ['version' => $version];
                if (($lang = $ctx['lang'] ?? '') !== '') {
                    $params['lang'] = $lang;
                }

                // Canonicalise to the versioned path list even when the version rode
                // in as a `?version=` query, so the redirect target is the single
                // canonical form for a historical patch.
                return $this->redirectToRoute($versionedListRoute, $params);
            }

            throw $this->createNotFoundException($e->getMessage(), $e);
        }

        return $this->redirectToHomeWithError($ctx, $e);
    }

    /**
     * The Data Dragon patch explicitly pinned in the current URL — the `/{version}/…`
     * path segment, else a `?version=` query — validated against the real version
     * list. Null when the URL pins nothing (patch defaulted from the session): the
     * signal that separates a historical URL (redirect to its list) from the clean
     * canonical URL (honest 404). Never falls back to the session.
     */
    private function urlPinnedVersion(): ?string
    {
        $request = $this->requestStack->getCurrentRequest();
        if ($request === null) {
            return null;
        }

        foreach (
            [$request->attributes->get('version'), $request->query->get('version')] as $candidate
        ) {
            if (
                is_string($candidate)
                && $candidate !== ''
                && $this->versionManager->versionExists($candidate)
            ) {
                return $candidate;
            }
        }

        return null;
    }

    /**
     * Immediate neighbours of an entry in its collection order — feeds the
     * previous/next navigation of the detail pages. Best-effort: any data error
     * yields an empty navigation, never a broken page. Edition-aware resources
     * also label each neighbour with its edition: walking the classic range of
     * the item list, every name repeats a current item's.
     *
     * @param array{version: string, lang: string} $selection page context the detail page
     *        was resolved against ({@see \App\Service\Client\PageContextResolver::selection()})
     * @return array{prev: ?array{id: string, name: string, edition: ?string},
     *               next: ?array{id: string, name: string, edition: ?string}}
     */
    protected function neighbors(
        AbstractManager $manager,
        array $selection,
        string $currentId
    ): array {
        try {
            $index = $manager->listIndex($selection['version'], $selection['lang']);
        } catch (\Throwable) {
            return ['prev' => null, 'next' => null];
        }

        // PHP recasts numeric-string array keys to int — normalise back so the
        // strict search matches route ids like "3153".
        $ids = array_map(strval(...), array_keys($index));
        $pos = array_search($currentId, $ids, true);
        if ($pos === false) {
            return ['prev' => null, 'next' => null];
        }

        $dataset = DatasetRef::fromSelection($selection);
        $at = static fn (int $i): ?array => isset($ids[$i])
            ? [
                'id'      => (string) $ids[$i],
                'name'    => $index[$ids[$i]],
                'edition' => $manager instanceof EditionAwareInterface
                    ? $manager->editionOfId((string) $ids[$i], $dataset)->value
                    : null,
            ]
            : null;

        return ['prev' => $at($pos - 1), 'next' => $at($pos + 1)];
    }
}
