<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\UserAgentParser;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class UserAgentParserTest extends TestCase
{
    private UserAgentParser $parser;

    protected function setUp(): void
    {
        $this->parser = new UserAgentParser();
    }

    public function testEmptyUserAgentIsOther(): void
    {
        $result = $this->parser->parse(null);

        self::assertSame('other', $result['browser']);
        self::assertSame('other', $result['os']);
        self::assertFalse($result['isBot']);
    }

    #[DataProvider('bots')]
    public function testBotsAreFlagged(string $ua): void
    {
        $result = $this->parser->parse($ua);

        self::assertTrue($result['isBot']);
        self::assertSame('bot', $result['device']);
    }

    public static function bots(): array
    {
        return [
            'googlebot' => ['Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)'],
            'bingbot' => ['Mozilla/5.0 (compatible; bingbot/2.0)'],
            'curl' => ['curl/8.4.0'],
            'headless' => ['Mozilla/5.0 HeadlessChrome/120.0'],
        ];
    }

    public function testEdgeIsNotMisreadAsChrome(): void
    {
        $ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0 Safari/537.36 Edg/120.0';

        self::assertSame('Edge', $this->parser->parse($ua)['browser']);
    }

    public function testChromeIsNotMisreadAsSafari(): void
    {
        $ua = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Chrome/120.0 Safari/537.36';

        self::assertSame('Chrome', $this->parser->parse($ua)['browser']);
    }

    public function testIphoneIsMobileIos(): void
    {
        $ua = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Version/17.0 Mobile/15E148 Safari/604.1';
        $result = $this->parser->parse($ua);

        self::assertSame('iOS', $result['os']);
        self::assertSame('mobile', $result['device']);
        self::assertSame('Safari', $result['browser']);
    }

    public function testAndroidTabletDetectedWhenNotMobile(): void
    {
        $ua = 'Mozilla/5.0 (Linux; Android 14; Tab S9) AppleWebKit/537.36 Chrome/120.0 Safari/537.36';

        self::assertSame('tablet', $this->parser->parse($ua)['device']);
    }

    public function testDesktopWindowsChrome(): void
    {
        $ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0 Safari/537.36';
        $result = $this->parser->parse($ua);

        self::assertSame('Windows', $result['os']);
        self::assertSame('desktop', $result['device']);
    }
}
