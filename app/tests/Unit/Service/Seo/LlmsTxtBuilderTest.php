<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo;

use App\Service\Seo\InventorySnapshot;
use App\Service\Seo\LlmsTxtBuilder;
use PHPUnit\Framework\TestCase;

final class LlmsTxtBuilderTest extends TestCase
{
    private const ORIGIN = 'https://league-of-data-base.com';

    private LlmsTxtBuilder $builder;

    protected function setUp(): void
    {
        $this->builder = new LlmsTxtBuilder('League Of Data Base');
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
        $text = $this->builder->build(self::ORIGIN, new InventorySnapshot('15.1.1', 170, null, 5, 18));

        self::assertStringContainsString('Current patch: 15.1.1.', $text);
        self::assertStringNotContainsString('It publishes', $text);
        self::assertStringNotContainsString(' 0 ', $text);
    }

    public function testDegradesGracefullyWhenTheUpstreamVersionIsUnavailable(): void
    {
        // A transient outage must still produce a usable brief, not a broken one.
        $text = $this->builder->build(self::ORIGIN, new InventorySnapshot('', null, null, null, null));

        self::assertStringContainsString('Current patch: unavailable', $text);
        self::assertStringContainsString('# League Of Data Base', $text);
        self::assertStringNotContainsString('It publishes', $text);
    }

    public function testLinksAreAbsoluteAndBuiltOnTheGivenOrigin(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());

        foreach (['/champions', '/objects', '/runes', '/summoners', '/about', '/about/data', '/faq'] as $path) {
            self::assertStringContainsString(self::ORIGIN . $path, $text);
        }
        self::assertStringNotContainsString('](/', $text);
    }

    public function testStatesTheCaveatsThatKeepEnginesFromOverclaiming(): void
    {
        $text = $this->builder->build(self::ORIGIN, $this->snapshot());

        self::assertStringContainsString('Not affiliated with, or endorsed by, Riot Games.', $text);
        self::assertStringContainsString('No win rates, pick rates, tier lists or match history', $text);
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
}
