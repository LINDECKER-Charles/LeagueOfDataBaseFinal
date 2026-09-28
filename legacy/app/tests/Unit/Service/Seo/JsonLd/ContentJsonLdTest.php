<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo\JsonLd;

use App\Service\Seo\JsonLd\ContentJsonLd;
use App\Service\Seo\JsonLd\JsonLdEncoder;
use PHPUnit\Framework\TestCase;

final class ContentJsonLdTest extends TestCase
{
    private const URL = 'https://example.com/faq';

    private ContentJsonLd $content;

    protected function setUp(): void
    {
        $this->content = new ContentJsonLd(new JsonLdEncoder());
    }

    public function testFaqPageNestsEachAnswerUnderItsQuestion(): void
    {
        $graph = $this->content->faqPage([
            ['question' => 'What is it?', 'answer' => 'A League of Legends encyclopedia.'],
            ['question' => 'Is it free?', 'answer' => 'Yes.'],
        ], self::URL);

        self::assertSame('FAQPage', $graph['@type']);
        self::assertCount(2, $graph['mainEntity']);
        self::assertSame('Question', $graph['mainEntity'][0]['@type']);
        self::assertSame('What is it?', $graph['mainEntity'][0]['name']);
        self::assertSame(
            ['@type' => 'Answer', 'text' => 'A League of Legends encyclopedia.'],
            $graph['mainEntity'][0]['acceptedAnswer'],
        );
    }

    public function testFaqPageSkipsIncompletePairsAndStripsMarkup(): void
    {
        $graph = $this->content->faqPage([
            ['question' => 'Answered', 'answer' => '  Yes, <b>really</b>.  '],
            ['question' => 'Unanswered', 'answer' => '   '],
            ['question' => '', 'answer' => 'Orphan answer'],
        ], self::URL);

        self::assertCount(1, $graph['mainEntity']);
        self::assertSame('Yes, really.', $graph['mainEntity'][0]['acceptedAnswer']['text']);
    }

    public function testDatasetStatesProvenanceVersionAndLanguages(): void
    {
        $graph = $this->content->dataset([
            'name'        => 'League of Legends game data — patch 15.1.1',
            'url'         => 'https://example.com/about/data',
            'description' => '  Champions, items, runes and summoner spells.  ',
            'version'     => '15.1.1',
            'languages'   => ['en_US', 'fr_FR'],
            'keywords'    => ['League of Legends', 'Data Dragon'],
            'creatorId'   => 'https://example.com/#organization',
        ]);

        self::assertSame('Dataset', $graph['@type']);
        self::assertSame('15.1.1', $graph['version']);
        self::assertSame(['en_US', 'fr_FR'], $graph['inLanguage']);
        self::assertSame(ContentJsonLd::DATA_SOURCE_URL, $graph['isBasedOn']);
        self::assertSame(['@id' => 'https://example.com/#organization'], $graph['creator']);
        self::assertTrue($graph['isAccessibleForFree']);
        self::assertSame('Champions, items, runes and summoner spells.', $graph['description']);
    }

    public function testDatasetOmitsWithheldOptionalFields(): void
    {
        $graph = $this->content->dataset([
            'name'        => 'Dataset',
            'url'         => 'https://example.com/about/data',
            'description' => 'Minimal.',
        ]);

        self::assertArrayNotHasKey('version', $graph);
        self::assertArrayNotHasKey('inLanguage', $graph);
        self::assertArrayNotHasKey('creator', $graph);
    }

    public function testAboutPageCarriesItsOwnIdentity(): void
    {
        $graph = $this->content->aboutPage([
            'name'        => 'About the project',
            'url'         => 'https://example.com/about',
            'description' => 'What this site is.',
            'inLanguage'  => 'fr',
        ]);

        self::assertSame('AboutPage', $graph['@type']);
        self::assertSame('https://example.com/about#about', $graph['@id']);
        self::assertSame('fr', $graph['inLanguage']);
    }
}
