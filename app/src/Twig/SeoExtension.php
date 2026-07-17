<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\Seo\JsonLdBuilder;
use App\Service\Seo\OgLocale;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * SEO primitives shared by every page: canonical URL, absolute URLs for social
 * cards, og:locale, the "{page} — {site}" title pattern and JSON-LD rendering.
 *
 * Canonical policy: scheme + host + path of the current request, query dropped.
 * The ?version&lang variants of a page are alternate renders of the same
 * document (the default render answers on the bare path), so they all point to
 * the query-less URL and never compete in the index.
 */
final class SeoExtension extends AbstractExtension
{
    /** Brand mark served to Organization consumers (512px PWA icon, always deployed). */
    private const ORGANIZATION_LOGO_PATH = '/favicon/icon-512.png';

    private const TITLE_SEPARATOR = ' — ';

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly JsonLdBuilder $jsonLd,
        private readonly OgLocale $ogLocale,
        #[Autowire('%legal.site_name%')]
        private readonly string $siteName,
    ) {}

    public function getFunctions(): array
    {
        return [
            new TwigFunction('seo_canonical', $this->canonical(...)),
            new TwigFunction('seo_absolute', $this->absolute(...)),
            new TwigFunction('seo_og_locale', $this->currentOgLocale(...)),
            new TwigFunction('seo_site_name', fn (): string => $this->siteName),
            new TwigFunction('seo_title', $this->title(...)),
            new TwigFunction('seo_jsonld', $this->jsonLdScript(...), ['is_safe' => ['html']]),
            new TwigFunction('seo_jsonld_global', $this->globalGraph(...), ['is_safe' => ['html']]),
            new TwigFunction('seo_breadcrumbs', $this->jsonLd->breadcrumbList(...)),
            new TwigFunction('seo_item_list', $this->jsonLd->itemList(...)),
            new TwigFunction('seo_game_character', $this->jsonLd->gameCharacter(...)),
            new TwigFunction('seo_game_item', $this->jsonLd->gameItem(...)),
        ];
    }

    /** Canonical URL of the current request — path only, no query string. */
    public function canonical(): string
    {
        $request = $this->request();

        return $request === null ? '' : $request->getSchemeAndHttpHost() . $request->getPathInfo();
    }

    /**
     * Absolute URL for a site path (og:image, sitemap-style needs). Already
     * absolute URLs pass through untouched so CDN-hosted images keep working.
     */
    public function absolute(string $path): string
    {
        if (preg_match('#^https?://#i', $path) === 1) {
            return $path;
        }

        $request = $this->request();
        if ($request === null) {
            return $path;
        }

        return $request->getSchemeAndHttpHost() . '/' . ltrim($path, '/');
    }

    /** og:locale for the resolved UI locale of the current request. */
    public function currentOgLocale(): string
    {
        $locale = $this->request()?->getLocale();

        return $this->ogLocale->fromUiLocale($locale ?? '');
    }

    /** "{page} — {site}" title pattern; an empty page title yields the bare site name. */
    public function title(string $pageTitle): string
    {
        $pageTitle = trim($pageTitle);

        return $pageTitle === '' ? $this->siteName : $pageTitle . self::TITLE_SEPARATOR . $this->siteName;
    }

    /** @param array<string,mixed> $data */
    public function jsonLdScript(array $data): string
    {
        return '<script type="application/ld+json">' . $this->jsonLd->encode($data) . '</script>';
    }

    /** Sitewide WebSite + Organization nodes, emitted once from the base layout. */
    public function globalGraph(): string
    {
        $root = $this->absolute('/');

        return $this->jsonLdScript($this->jsonLd->webSite($this->siteName, $root))
            . $this->jsonLdScript($this->jsonLd->organization(
                $this->siteName,
                $root,
                $this->absolute(self::ORGANIZATION_LOGO_PATH),
            ));
    }

    private function request(): ?Request
    {
        return $this->requestStack->getCurrentRequest();
    }
}
