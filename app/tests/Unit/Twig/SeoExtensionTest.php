<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Service\Seo\JsonLdBuilder;
use App\Service\Seo\OgLocale;
use App\Twig\SeoExtension;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

final class SeoExtensionTest extends TestCase
{
    private const SITE_NAME = 'League Of Data Base';

    private RequestStack $requestStack;
    private SeoExtension $extension;

    protected function setUp(): void
    {
        $this->requestStack = new RequestStack();
        $this->extension = new SeoExtension(
            $this->requestStack,
            new JsonLdBuilder(),
            new OgLocale(),
            self::SITE_NAME,
        );
    }

    public function testCanonicalDropsTheQueryString(): void
    {
        $this->requestStack->push(Request::create('http://localhost:8080/champion/Aatrox?version=15.1.1&lang=fr_FR'));

        self::assertSame('http://localhost:8080/champion/Aatrox', $this->extension->canonical());
    }

    public function testCanonicalIsEmptyOutsideARequest(): void
    {
        self::assertSame('', $this->extension->canonical());
    }

    public function testAbsolutePrefixesTheRequestHostAndNormalizesSlashes(): void
    {
        $this->requestStack->push(Request::create('https://league-of-data-base.com/champions'));

        self::assertSame('https://league-of-data-base.com/preview/home.png', $this->extension->absolute('/preview/home.png'));
        self::assertSame('https://league-of-data-base.com/cdn/blobs/abc.png', $this->extension->absolute('cdn/blobs/abc.png'));
    }

    public function testAbsolutePassesThroughAlreadyAbsoluteUrls(): void
    {
        $this->requestStack->push(Request::create('https://league-of-data-base.com/'));
        $cdn = 'https://ddragon.leagueoflegends.com/cdn/img/champion/splash/Aatrox_0.jpg';

        self::assertSame($cdn, $this->extension->absolute($cdn));
    }

    public function testTitleAppendsTheSiteNameAndFallsBackToItWhenEmpty(): void
    {
        self::assertSame('Aatrox, LoL champion — League Of Data Base', $this->extension->title('Aatrox, LoL champion'));
        self::assertSame(self::SITE_NAME, $this->extension->title('   '));
    }

    public function testCurrentOgLocaleReadsTheRequestLocale(): void
    {
        $request = Request::create('http://localhost:8080/');
        $request->setLocale('fr');
        $this->requestStack->push($request);

        self::assertSame('fr_FR', $this->extension->currentOgLocale());
    }

    public function testJsonLdScriptWrapsEncodedDataInAScriptTag(): void
    {
        $html = $this->extension->jsonLdScript(['@type' => 'WebSite', 'name' => 'x']);

        self::assertStringStartsWith('<script type="application/ld+json">', $html);
        self::assertStringEndsWith('</script>', $html);
    }

    public function testGlobalGraphEmitsWebSiteAndOrganizationNodes(): void
    {
        $this->requestStack->push(Request::create('https://league-of-data-base.com/'));

        $html = $this->extension->globalGraph();

        self::assertStringContainsString('"WebSite"', $html);
        self::assertStringContainsString('"Organization"', $html);
        self::assertStringContainsString('https://league-of-data-base.com/favicon/icon-512.png', $html);
        self::assertSame(2, substr_count($html, '<script type="application/ld+json">'));
    }
}
