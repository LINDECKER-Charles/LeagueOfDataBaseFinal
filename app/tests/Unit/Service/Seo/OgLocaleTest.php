<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo;

use App\Service\Seo\OgLocale;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class OgLocaleTest extends TestCase
{
    private OgLocale $ogLocale;

    protected function setUp(): void
    {
        $this->ogLocale = new OgLocale();
    }

    /** @return iterable<string, array{string, string}> */
    public static function localeProvider(): iterable
    {
        yield 'french'                => ['fr', 'fr_FR'];
        yield 'english'               => ['en', 'en_US'];
        yield 'portuguese is brazil'  => ['pt', 'pt_BR'];
        yield 'simplified chinese'    => ['zh_Hans', 'zh_CN'];
        yield 'traditional chinese'   => ['zh_Hant', 'zh_TW'];
        yield 'arabic'                => ['ar', 'ar_AE'];
    }

    #[DataProvider('localeProvider')]
    public function testMapsUiLocaleToOgTerritoryCode(string $ui, string $expected): void
    {
        self::assertSame($expected, $this->ogLocale->fromUiLocale($ui));
    }

    public function testUnknownLocaleFallsBackToDefault(): void
    {
        self::assertSame(OgLocale::DEFAULT, $this->ogLocale->fromUiLocale('xx'));
        self::assertSame(OgLocale::DEFAULT, $this->ogLocale->fromUiLocale(''));
    }
}
