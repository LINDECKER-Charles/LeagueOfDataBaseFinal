<?php
declare(strict_types=1);

namespace App\Tests\Unit\Twig;

use App\Service\Seo\CanonicalHost;
use App\Service\Seo\JsonLd\ContentJsonLd;
use App\Service\Seo\JsonLd\GameEntityJsonLd;
use App\Service\Seo\JsonLd\JsonLdBuilder;
use App\Service\Seo\JsonLd\JsonLdEncoder;
use App\Service\Seo\OgLocale;
use App\Service\Seo\JsonLd\SiteGraphJsonLd;
use App\Twig\SeoExtension;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Contracts\Translation\TranslatorInterface;

final class SeoExtensionTest extends TestCase
{
    private const SITE_NAME = 'League Of Data Base';
    private const CANONICAL_HOST = 'league-of-data-base.com';
    private const MIRROR_HOST = 'league-of-data-base.fr';

    private RequestStack $requestStack;
    private SeoExtension $extension;

    protected function setUp(): void
    {
        $this->requestStack = new RequestStack();
        $this->extension = $this->seoExtension($this->interpolatingTranslator());
    }

    /** Interpolates like the real translator so the versioned-title suffix is exercised. */
    private function interpolatingTranslator(): TranslatorInterface
    {
        $translator = $this->createStub(TranslatorInterface::class);
        $translator->method('trans')->willReturnCallback(
            static function (string $id, array $params = []): string {
                $catalogue = [
                    'versioned_suffix'  => '(patch %version%)',
                    'base.description'  => 'The League of Legends encyclopaedia.',
                ];

                return strtr($catalogue[$id] ?? $id, $params);
            },
        );

        return $translator;
    }

    private function seoExtension(TranslatorInterface $translator): SeoExtension
    {
        $encoder = new JsonLdEncoder();

        return new SeoExtension(
            $this->requestStack,
            new JsonLdBuilder($encoder),
            new SiteGraphJsonLd($encoder, self::SITE_NAME, ['https://github.com/example']),
            new GameEntityJsonLd($encoder),
            new ContentJsonLd($encoder),
            new CanonicalHost(self::CANONICAL_HOST, [self::CANONICAL_HOST, self::MIRROR_HOST]),
            new OgLocale(),
            $translator,
            self::SITE_NAME,
        );
    }

    public function testCanonicalDropsTheQueryString(): void
    {
        $this->requestStack->push(
            Request::create('http://localhost:8080/champion/Aatrox?version=15.1.1&lang=fr_FR')
        );

        self::assertSame('http://localhost:8080/champion/Aatrox', $this->extension->canonical());
    }

    public function testCanonicalCollapsesTheWwwHost(): void
    {
        // www and the apex serve identical content; both must canonicalize to one URL.
        $this->requestStack->push(Request::create('https://www.league-of-data-base.com/champions'));

        self::assertSame(
            'https://league-of-data-base.com/champions',
            $this->extension->canonical()
        );
    }

    public function testCanonicalIsEmptyOutsideARequest(): void
    {
        self::assertSame('', $this->extension->canonical());
    }

    public function testAbsolutePrefixesTheRequestHostAndNormalizesSlashes(): void
    {
        $this->requestStack->push(Request::create('https://league-of-data-base.com/champions'));

        self::assertSame(
            'https://league-of-data-base.com/preview/home.png',
            $this->extension->absolute('/preview/home.png')
        );
        self::assertSame(
            'https://league-of-data-base.com/cdn/blobs/abc.png',
            $this->extension->absolute('cdn/blobs/abc.png')
        );
    }

    public function testAbsolutePassesThroughAlreadyAbsoluteUrls(): void
    {
        $this->requestStack->push(Request::create('https://league-of-data-base.com/'));
        $cdn = 'https://ddragon.leagueoflegends.com/cdn/img/champion/splash/Aatrox_0.jpg';

        self::assertSame($cdn, $this->extension->absolute($cdn));
    }

    public function testCanonicalFoldsMirrorDomainsIntoThePreferredHost(): void
    {
        // .fr serves identical content — it must not compete as a duplicate.
        $this->requestStack->push(
            Request::create('https://www.league-of-data-base.fr/champion/Aatrox')
        );

        self::assertSame(
            'https://league-of-data-base.com/champion/Aatrox',
            $this->extension->canonical()
        );
    }

    public function testCanonicalLeavesUnknownHostsSelfCanonical(): void
    {
        // Dev/staging/previews must never canonicalize to production.
        $this->requestStack->push(Request::create('http://localhost:8080/champions'));

        self::assertSame('http://localhost:8080/champions', $this->extension->canonical());
    }

    public function testTitleAppendsTheSiteNameAndFallsBackToItWhenEmpty(): void
    {
        self::assertSame(
            'Aatrox, LoL champion — League Of Data Base',
            $this->extension->title('Aatrox, LoL champion')
        );
        self::assertSame(self::SITE_NAME, $this->extension->title('   '));
    }

    public function testTitleWeavesInThePatchOnAVersionedRoute(): void
    {
        $request = Request::create('http://localhost:8080/13.1.1/champion/Aatrox');
        $request->attributes->set('_route', 'app_champion_versioned');
        $request->attributes->set('version', '13.1.1');
        $this->requestStack->push($request);

        self::assertSame(
            'Aatrox, LoL champion (patch 13.1.1) — League Of Data Base',
            $this->extension->title('Aatrox, LoL champion'),
        );
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

    public function testGlobalGraphEmitsOneLinkedGraphCarryingThePageTitle(): void
    {
        $request = Request::create('https://league-of-data-base.com/champions');
        $request->setLocale('en');
        $this->requestStack->push($request);

        $html = $this->extension->globalGraph('Champions — League Of Data Base');

        // A single script holding one @graph — that is what lets the @id
        // cross-references resolve into one entity.
        self::assertSame(1, substr_count($html, '<script type="application/ld+json">'));

        $graph = json_decode(strip_tags($html), true)['@graph'];
        self::assertSame(['WebSite', 'Organization', 'WebPage'], array_column($graph, '@type'));
        self::assertSame(
            'https://league-of-data-base.com/#organization',
            $graph[0]['publisher']['@id']
        );
        self::assertSame(
            'https://league-of-data-base.com/favicon/icon-512.png',
            $graph[1]['logo']['url']
        );
        self::assertSame('Champions — League Of Data Base', $graph[2]['name']);
        self::assertSame('https://league-of-data-base.com/#website', $graph[2]['isPartOf']['@id']);
    }

    public function testGlobalGraphIsEmptyOutsideARequest(): void
    {
        self::assertSame('', $this->extension->globalGraph('Anything'));
    }
}
