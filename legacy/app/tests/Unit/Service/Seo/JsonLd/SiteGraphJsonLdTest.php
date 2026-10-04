<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo\JsonLd;

use App\Service\Seo\JsonLd\JsonLdEncoder;
use App\Service\Seo\JsonLd\SiteGraphContext;
use App\Service\Seo\JsonLd\SiteGraphJsonLd;
use PHPUnit\Framework\TestCase;

final class SiteGraphJsonLdTest extends TestCase
{
    private const ROOT = 'https://example.com/';
    private const SAME_AS = ['https://github.com/example/project'];

    private SiteGraphJsonLd $graph;

    protected function setUp(): void
    {
        $this->graph = new SiteGraphJsonLd(
            new JsonLdEncoder(),
            'League Of Data Base',
            self::SAME_AS
        );
    }

    public function testGraphCrossReferencesItsNodesById(): void
    {
        $nodes = $this->graph->graph($this->context())['@graph'];
        [$site, $organization, $page] = $nodes;

        // The point of a graph: consumers resolve these refs into ONE entity.
        self::assertSame(self::ROOT . '#website', $site['@id']);
        self::assertSame(self::ROOT . '#organization', $organization['@id']);
        self::assertSame($organization['@id'], $site['publisher']['@id']);
        self::assertSame($site['@id'], $page['isPartOf']['@id']);
        self::assertSame('https://example.com/champions#webpage', $page['@id']);
    }

    public function testGraphCarriesOneContextForEveryNode(): void
    {
        $document = $this->graph->graph($this->context());

        self::assertSame(JsonLdEncoder::SCHEMA_CONTEXT, $document['@context']);
        self::assertCount(3, $document['@graph']);
        foreach ($document['@graph'] as $node) {
            self::assertArrayNotHasKey('@context', $node);
        }
    }

    public function testOrganizationCarriesSameAsAndALogoObject(): void
    {
        $organization = $this->graph->graph($this->context())['@graph'][1];

        self::assertSame(self::SAME_AS, $organization['sameAs']);
        self::assertSame('ImageObject', $organization['logo']['@type']);
        self::assertSame('https://example.com/favicon/icon-512.png', $organization['logo']['url']);
    }

    public function testEmptyOptionalValuesArePruned(): void
    {
        $bare = new SiteGraphJsonLd(new JsonLdEncoder(), 'League Of Data Base', []);

        $nodes = $bare->graph(new SiteGraphContext(
            root: self::ROOT,
            pageUrl: self::ROOT,
            pageName: '',
            siteDescription: '',
            logoUrl: '',
            locale: 'en',
        ))['@graph'];

        self::assertArrayNotHasKey('sameAs', $nodes[1]);
        self::assertArrayNotHasKey('description', $nodes[0]);
        self::assertArrayNotHasKey('name', $nodes[2]);
    }

    private function context(): SiteGraphContext
    {
        return new SiteGraphContext(
            root: self::ROOT,
            pageUrl: 'https://example.com/champions',
            pageName: 'Champions',
            siteDescription: 'The League of Legends encyclopaedia.',
            logoUrl: 'https://example.com/favicon/icon-512.png',
            locale: 'en',
        );
    }
}
