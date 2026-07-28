<?php
declare(strict_types=1);

namespace App\Tests\Functional;

use App\Service\Seo\SiteInventory;
use PHPUnit\Framework\Attributes\DataProvider;
use Symfony\Bundle\FrameworkBundle\Test\WebTestCase;

/**
 * Smoke coverage of the discoverability surface: the editorial pages (/about,
 * /about/data, /faq) and /llms.txt.
 *
 * These render translated copy and structured data assembled from several
 * sources; template lint cannot catch a missing variable or a stale key, and a
 * broken /about is invisible from the resource pages. One request per route is
 * enough to notice.
 *
 * The HTML pages need the Data Dragon datasets: the shared header builds its
 * navigation from the patch list, so *every* page of the site 500s without them
 * (CI runs PHP alone — no MinIO, no go-fetcher). Those cases skip rather than
 * fail, so a red result here always means a real regression. /llms.txt is
 * exempt: it degrades to "patch unavailable" by design and must prove it.
 */
final class EditorialPagesTest extends WebTestCase
{
    /** @return iterable<string, array{0:string}> */
    public static function editorialRoutes(): iterable
    {
        yield 'about'      => ['/about'];
        yield 'about data' => ['/about/data'];
        yield 'faq'        => ['/faq'];
    }

    #[DataProvider('editorialRoutes')]
    public function testEditorialPageRendersWithATitleAndCanonical(string $path): void
    {
        $client = static::createClient();
        $this->skipWithoutDatasets();

        $client->request('GET', $path);

        self::assertResponseIsSuccessful();
        self::assertSelectorExists('h1');
        self::assertSelectorExists('link[rel="canonical"]');
    }

    public function testFaqEmitsItsAnswersAsStructuredData(): void
    {
        $client = static::createClient();
        $this->skipWithoutDatasets();

        $crawler = $client->request('GET', '/faq');

        $faq = $this->structuredData($crawler, '"FAQPage"');
        self::assertCount(1, $faq, 'the FAQ page must emit exactly one FAQPage node');

        $graph = json_decode($faq[0], true, flags: JSON_THROW_ON_ERROR);
        self::assertNotEmpty($graph['mainEntity']);
        // Every rendered question must carry a non-empty answer.
        foreach ($graph['mainEntity'] as $question) {
            self::assertNotSame('', trim($question['name']));
            self::assertNotSame('', trim($question['acceptedAnswer']['text']));
        }
    }

    public function testEveryPageCarriesTheLinkedSiteGraph(): void
    {
        $client = static::createClient();
        $this->skipWithoutDatasets();

        $crawler = $client->request('GET', '/about');

        $site = $this->structuredData($crawler, '"@graph"');
        self::assertCount(1, $site);

        $nodes = json_decode($site[0], true, flags: JSON_THROW_ON_ERROR)['@graph'];
        self::assertSame(['WebSite', 'Organization', 'WebPage'], array_column($nodes, '@type'));
    }

    public function testLlmsTxtIsServedAsMarkdownAndDescribesTheProject(): void
    {
        $client = static::createClient();
        $client->request('GET', '/llms.txt');

        self::assertResponseIsSuccessful();
        self::assertResponseHeaderSame('Content-Type', 'text/markdown; charset=UTF-8');

        $body = (string) $client->getResponse()->getContent();
        self::assertStringStartsWith('# ', $body);
        self::assertStringContainsString('Current patch:', $body);
        self::assertStringContainsString('/about', $body);
    }

    /**
     * @return list<string> the ld+json payloads containing $needle
     */
    private function structuredData(\Symfony\Component\DomCrawler\Crawler $crawler, string $needle): array
    {
        $scripts = $crawler->filter('script[type="application/ld+json"]')->each(
            static fn (\Symfony\Component\DomCrawler\Crawler $node): string => $node->text(),
        );

        return array_values(array_filter($scripts, static fn (string $json): bool => str_contains($json, $needle)));
    }

    /** Skips when no patch can be resolved — an environment gap, not a regression. */
    private function skipWithoutDatasets(): void
    {
        $inventory = static::getContainer()->get(SiteInventory::class);
        \assert($inventory instanceof SiteInventory);

        if ($inventory->latestVersion() === '') {
            self::markTestSkipped('Data Dragon datasets unavailable (no MinIO/go-fetcher in this environment).');
        }
    }
}
