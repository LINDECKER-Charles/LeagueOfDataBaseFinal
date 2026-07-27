<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * Generic schema.org nodes reused across pages: navigation (BreadcrumbList,
 * ItemList) and people-authored content (Person, ProfilePage, Article).
 *
 * Sibling builders own the rest of the vocabulary: {@see SiteGraphJsonLd} for
 * the sitewide WebSite/Organization/WebPage graph, {@see GameEntityJsonLd} for
 * Data Dragon entities, {@see ContentJsonLd} for the editorial pages. Encoding
 * and pruning live in {@see JsonLdEncoder}.
 */
final class JsonLdBuilder
{
    public const SCHEMA_CONTEXT = JsonLdEncoder::SCHEMA_CONTEXT;

    /** Enough entries for rich results without bloating the render of 600-item lists. */
    public const ITEM_LIST_MAX = 20;

    public function __construct(
        private readonly JsonLdEncoder $encoder,
    ) {}

    /** @param array<string,mixed> $data */
    public function encode(array $data): string
    {
        return $this->encoder->encode($data);
    }

    /**
     * @param list<array{name:string, url:string}> $crumbs ordered root → current page
     * @return array<string,mixed>
     */
    public function breadcrumbList(array $crumbs): array
    {
        $elements = [];
        foreach (array_values($crumbs) as $i => $crumb) {
            $elements[] = [
                '@type'    => 'ListItem',
                'position' => $i + 1,
                'name'     => (string) ($crumb['name'] ?? ''),
                'item'     => (string) ($crumb['url'] ?? ''),
            ];
        }

        return [
            '@context'        => self::SCHEMA_CONTEXT,
            '@type'           => 'BreadcrumbList',
            'itemListElement' => $elements,
        ];
    }

    /**
     * @param list<array{name:string, url:string}> $items capped to {@see self::ITEM_LIST_MAX}
     * @return array<string,mixed>
     */
    public function itemList(array $items): array
    {
        $elements = [];
        foreach (array_values(\array_slice($items, 0, self::ITEM_LIST_MAX)) as $i => $item) {
            $elements[] = [
                '@type'    => 'ListItem',
                'position' => $i + 1,
                'name'     => (string) ($item['name'] ?? ''),
                'url'      => (string) ($item['url'] ?? ''),
            ];
        }

        return [
            '@context'        => self::SCHEMA_CONTEXT,
            '@type'           => 'ItemList',
            'numberOfItems'   => \count($elements),
            'itemListElement' => $elements,
        ];
    }

    /**
     * schema.org Person node — the subject of a {@see profilePage} or the author
     * of an {@see article}. Optional url/image/description are pruned when absent.
     *
     * @return array<string,mixed>
     */
    public function person(string $name, ?string $url = null, ?string $imageUrl = null, ?string $description = null): array
    {
        return $this->encoder->prune([
            '@type'       => 'Person',
            'name'        => $name,
            'url'         => $url,
            'image'       => $imageUrl,
            'description' => $description !== null ? trim($description) : null,
        ]);
    }

    /**
     * schema.org ProfilePage for a public summoner card. mainEntity is the
     * Person the page is about; the owner's builds ride along as a separate
     * ItemList node (see {@see itemList}), never nested here.
     *
     * @param array{name:string, url:string, image?:?string, description?:?string} $profile
     * @return array<string,mixed>
     */
    public function profilePage(array $profile): array
    {
        return [
            '@context'   => self::SCHEMA_CONTEXT,
            '@type'      => 'ProfilePage',
            'mainEntity' => $this->person(
                (string) $profile['name'],
                $profile['url'] ?? null,
                $profile['image'] ?? null,
                $profile['description'] ?? null,
            ),
        ];
    }

    /**
     * schema.org Article for a shared build. Every optional identity field is
     * pruned when absent, so a caller may withhold author/dates (e.g. a private,
     * noindex build renders no Article at all). author/about are nested nodes
     * built on demand.
     *
     * @param array{
     *   name:string, url:string, description?:?string, inLanguage?:?string,
     *   datePublished?:?string, dateModified?:?string,
     *   authorName?:?string, authorUrl?:?string, about?:?string
     * } $fields
     * @return array<string,mixed>
     */
    public function article(array $fields): array
    {
        $author = ($fields['authorName'] ?? '') !== ''
            ? $this->person((string) $fields['authorName'], $fields['authorUrl'] ?? null)
            : null;
        $about = ($fields['about'] ?? '') !== ''
            ? ['@type' => 'Thing', 'name' => (string) $fields['about']]
            : null;

        return $this->encoder->prune([
            '@context'      => self::SCHEMA_CONTEXT,
            '@type'         => 'Article',
            'headline'      => (string) $fields['name'],
            'url'           => (string) $fields['url'],
            'description'   => isset($fields['description']) ? trim((string) $fields['description']) : null,
            'inLanguage'    => $fields['inLanguage'] ?? null,
            'datePublished' => $fields['datePublished'] ?? null,
            'dateModified'  => $fields['dateModified'] ?? null,
            'author'        => $author,
            'about'         => $about,
        ]);
    }
}
