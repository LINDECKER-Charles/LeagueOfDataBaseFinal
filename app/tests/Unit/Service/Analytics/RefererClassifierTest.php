<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\Model\RefererSource;
use App\Service\Analytics\RefererClassifier;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class RefererClassifierTest extends TestCase
{
    private RefererClassifier $classifier;

    protected function setUp(): void
    {
        $this->classifier = new RefererClassifier();
    }

    public function testEmptyRefererIsDirect(): void
    {
        $result = $this->classifier->classify(null, 'league-of-data-base.fr');

        self::assertNull($result->host);
        self::assertSame(RefererSource::Direct, $result->source);
    }

    public function testSameHostIsInternal(): void
    {
        $result = $this->classifier->classify(
            'https://league-of-data-base.fr/champions',
            'league-of-data-base.fr'
        );

        self::assertSame('league-of-data-base.fr', $result->host);
        self::assertSame(RefererSource::Internal, $result->source);
    }

    public function testSubdomainOfAppHostIsInternal(): void
    {
        $result = $this->classifier->classify('https://www.example.com/x', 'example.com');

        self::assertSame(RefererSource::Internal, $result->source);
    }

    #[DataProvider('sources')]
    public function testSourceClassification(string $referer, RefererSource $expected): void
    {
        self::assertSame($expected, $this->classifier->classify($referer, 'lodb.fr')->source);
    }

    public static function sources(): array
    {
        return [
            'google' => ['https://www.google.com/search?q=lol', RefererSource::Search],
            'bing' => ['https://www.bing.com/search', RefererSource::Search],
            'twitter' => ['https://twitter.com/x', RefererSource::Social],
            'reddit' => ['https://www.reddit.com/r/lol', RefererSource::Social],
            'unknown' => ['https://some-blog.example/post', RefererSource::External],
        ];
    }

    /** The persisted `refSource` values are a contract with the stored NDJSON. */
    public function testSourceValuesAreStable(): void
    {
        self::assertSame('direct', RefererSource::Direct->value);
        self::assertSame('internal', RefererSource::Internal->value);
        self::assertSame('search', RefererSource::Search->value);
        self::assertSame('social', RefererSource::Social->value);
        self::assertSame('external', RefererSource::External->value);
    }

    public function testHostIsLowercasedAndExtracted(): void
    {
        $result = $this->classifier->classify('https://Google.COM/path?x=1', 'lodb.fr');

        self::assertSame('google.com', $result->host);
    }
}
