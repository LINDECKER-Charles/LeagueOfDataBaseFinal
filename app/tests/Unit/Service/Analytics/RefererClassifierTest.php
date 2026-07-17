<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

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

        self::assertNull($result['host']);
        self::assertSame(RefererClassifier::DIRECT, $result['source']);
    }

    public function testSameHostIsInternal(): void
    {
        $result = $this->classifier->classify('https://league-of-data-base.fr/champions', 'league-of-data-base.fr');

        self::assertSame('league-of-data-base.fr', $result['host']);
        self::assertSame(RefererClassifier::INTERNAL, $result['source']);
    }

    public function testSubdomainOfAppHostIsInternal(): void
    {
        $result = $this->classifier->classify('https://www.example.com/x', 'example.com');

        self::assertSame(RefererClassifier::INTERNAL, $result['source']);
    }

    #[DataProvider('sources')]
    public function testSourceClassification(string $referer, string $expected): void
    {
        self::assertSame($expected, $this->classifier->classify($referer, 'lodb.fr')['source']);
    }

    public static function sources(): array
    {
        return [
            'google' => ['https://www.google.com/search?q=lol', RefererClassifier::SEARCH],
            'bing' => ['https://www.bing.com/search', RefererClassifier::SEARCH],
            'twitter' => ['https://twitter.com/x', RefererClassifier::SOCIAL],
            'reddit' => ['https://www.reddit.com/r/lol', RefererClassifier::SOCIAL],
            'unknown' => ['https://some-blog.example/post', RefererClassifier::EXTERNAL],
        ];
    }

    public function testHostIsLowercasedAndExtracted(): void
    {
        $result = $this->classifier->classify('https://Google.COM/path?x=1', 'lodb.fr');

        self::assertSame('google.com', $result['host']);
    }
}
