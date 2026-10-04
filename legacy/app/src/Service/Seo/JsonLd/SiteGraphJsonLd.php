<?php
declare(strict_types=1);

namespace App\Service\Seo\JsonLd;

/**
 * Sitewide schema.org graph: WebSite, Organization and the current WebPage,
 * emitted as one `@graph` whose nodes cross-reference each other by `@id`.
 *
 * Why a graph and not three loose nodes: consumers (search engines, answer
 * engines) resolve `publisher` / `isPartOf` references to build a single entity
 * for the site. Independent nodes repeating the same name are read as unrelated
 * fragments, which is what keeps a site from being recognised as an entity.
 *
 * No SearchAction is emitted: the site has no server-rendered search-results
 * URL (list filtering is client-side), and a SearchAction pointing at a page
 * that does not answer the query is invalid.
 */
final class SiteGraphJsonLd
{
    private const WEBSITE_ID = '#website';
    private const ORGANIZATION_ID = '#organization';
    private const WEBPAGE_ID = '#webpage';

    /**
     * @param list<string> $sameAs authoritative profiles corroborating the publisher's identity
     */
    public function __construct(
        private readonly JsonLdEncoder $encoder,
        private readonly string $siteName,
        private readonly array $sameAs,
    ) {}

    /** @return array<string,mixed> */
    public function graph(SiteGraphContext $context): array
    {
        return $this->encoder->graph([
            $this->webSite($context),
            $this->organization($context),
            $this->webPage($context),
        ]);
    }

    /** @return array<string,mixed> */
    private function webSite(SiteGraphContext $context): array
    {
        return $this->encoder->prune([
            '@type'       => 'WebSite',
            '@id'         => $context->root . self::WEBSITE_ID,
            'url'         => $context->root,
            'name'        => $this->siteName,
            'description' => $context->siteDescription,
            'inLanguage'  => $context->locale,
            'publisher'   => ['@id' => $context->root . self::ORGANIZATION_ID],
        ]);
    }

    /** @return array<string,mixed> */
    private function organization(SiteGraphContext $context): array
    {
        return $this->encoder->prune([
            '@type'       => 'Organization',
            '@id'         => $context->root . self::ORGANIZATION_ID,
            'name'        => $this->siteName,
            'url'         => $context->root,
            'description' => $context->siteDescription,
            'logo'        => [
                '@type' => 'ImageObject',
                '@id'   => $context->root . '#logo',
                'url'   => $context->logoUrl,
            ],
            'sameAs' => array_values($this->sameAs),
        ]);
    }

    /** @return array<string,mixed> */
    private function webPage(SiteGraphContext $context): array
    {
        return $this->encoder->prune([
            '@type'      => 'WebPage',
            '@id'        => $context->pageUrl . self::WEBPAGE_ID,
            'url'        => $context->pageUrl,
            'name'       => $context->pageName,
            'isPartOf'   => ['@id' => $context->root . self::WEBSITE_ID],
            'inLanguage' => $context->locale,
        ]);
    }
}
