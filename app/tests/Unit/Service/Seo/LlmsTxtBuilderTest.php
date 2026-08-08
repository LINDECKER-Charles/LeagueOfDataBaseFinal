<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo;

use App\Service\Seo\Inventory\InventorySnapshot;
use App\Service\Seo\LlmsTxtBuilder;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Component\Routing\Route;
use Symfony\Component\Routing\RouteCollection;

final class LlmsTxtBuilderTest extends TestCase
{
    private const ORIGIN = 'https://league-of-data-base.com';

    /** Route name => path, as declared by the controllers the brief links to. */
    private const ROUTES = [
        'app_champions' => '/champions',
        'app_items' => '/objects',
        'app_runes' => '/runes',
        'app_summoners' => '/summoners',
        'app_about' => '/about',
        'app_about_data' => '/about/data',
        'app_faq' => '/faq',
        'app_developers' => '/developers',
        'app_changelog' => '/changelog',
    ];

    private LlmsTxtBuilder $builder;

    protected function setUp(): void
    {
        $this->builder = new LlmsTxtBuilder($this->router(), 'League Of Data Base');
    }

    public function testOpensWithTheLlmsTxtHeaderAndSummary(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());
        $lines = explode("\n", $text);

        // llmstxt.org shape: an H1 title, then a blockquote summary.
        self::assertSame('# League Of Data Base', $lines[0]);
        self::assertStringStartsWith('> ', $lines[2]);
    }

    public function testStatesTheLivePatchAndItsCounts(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());

        self::assertStringContainsString('Current patch: 15.1.1.', $text);
        self::assertStringContainsString(
            'It publishes 170 champions, 412 items, 5 rune paths and 18 summoner spells.',
            $text,
        );
    }

    public function testDropsTheCountsRatherThanQuotingZerosWhenASectionIsUnreadable(): void
    {
        // An engine would quote "0 items" back as fact — say nothing instead.
        $text = $this->builder->build(
            self::ORIGIN,
            new InventorySnapshot('15.1.1', 170, null, 5, 18),
        );

        self::assertStringContainsString('Current patch: 15.1.1.', $text);
        self::assertStringNotContainsString('It publishes', $text);
        self::assertStringNotContainsString(' 0 ', $text);
    }

    public function testDegradesGracefullyWhenTheUpstreamVersionIsUnavailable(): void
    {
        // A transient outage must still produce a usable brief, not a broken one.
        $text = $this->builder->build(
            self::ORIGIN,
            new InventorySnapshot('', null, null, null, null),
        );

        self::assertStringContainsString('Current patch: unavailable', $text);
        self::assertStringContainsString('# League Of Data Base', $text);
        self::assertStringNotContainsString('It publishes', $text);
    }

    public function testLinksAreAbsoluteAndBuiltOnTheGivenOrigin(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());

        foreach ([
            '/champions', '/objects', '/runes', '/summoners', '/about', '/about/data', '/faq',
        ] as $path) {
            self::assertStringContainsString(self::ORIGIN . $path, $text);
        }
        self::assertStringNotContainsString('](/', $text);
    }

    public function testStatesTheCaveatsThatKeepEnginesFromOverclaiming(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());

        self::assertStringContainsString('Not affiliated with, or endorsed by, Riot Games.', $text);
        self::assertStringContainsString(
            'No win rates, pick rates, tier lists or match history',
            $text,
        );
    }

    public function testEndsWithASingleTrailingNewline(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());

        self::assertStringEndsWith("\n", $text);
        self::assertStringEndsNotWith("\n\n", $text);
    }

    private function snapshot(): InventorySnapshot
    {
        return new InventorySnapshot('15.1.1', 170, 412, 5, 18);
    }

    /** Real generator over the routes under test — a stub would prove nothing. */
    private function router(): UrlGeneratorInterface
    {
        $collection = new RouteCollection();
        foreach (self::ROUTES as $name => $path) {
            $collection->add($name, new Route($path));
        }

        return new \Symfony\Component\Routing\Generator\UrlGenerator(
            $collection,
            new \Symfony\Component\Routing\RequestContext(),
        );
    }
}
