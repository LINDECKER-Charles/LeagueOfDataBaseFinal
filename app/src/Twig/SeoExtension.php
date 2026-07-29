<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\Seo\CanonicalHost;
use App\Service\Seo\ContentJsonLd;
use App\Service\Seo\GameEntityJsonLd;
use App\Service\Seo\JsonLdBuilder;
use App\Service\Seo\OgLocale;
use App\Service\Seo\SiteGraphContext;
use App\Service\Seo\SiteGraphJsonLd;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Contracts\Translation\TranslatorInterface;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * SEO primitives shared by every page: canonical URL, absolute URLs for social
 * cards, og:locale, the "{page} — {site}" title pattern and JSON-LD rendering.
 * Node construction itself lives in the App\Service\Seo builders; this class
 * only wires them to Twig and resolves request state.
 *
 * Canonical policy: scheme + canonical host + path of the current request, with
 * the query dropped and mirror domains folded ({@see CanonicalHost}). The
 * ?version&lang variants of a page are alternate renders of the same document
 * (the default render answers on the bare path), so they all point to the
 * query-less URL and never compete in the index.
 */
final class SeoExtension extends AbstractExtension
{
    /** Brand mark served to Organization consumers (512px PWA icon, always deployed). */
    private const ORGANIZATION_LOGO_PATH = '/favicon/icon-512.png';

    private const TITLE_SEPARATOR = ' — ';

    /** Suffix marking the `/{version}/…` variant of a resource route. */
    private const VERSIONED_SUFFIX = '_versioned';

    /** Sitewide pitch, already shipped in all 21 catalogs. */
    private const SITE_DESCRIPTION_KEY = 'base.description';

    public function __construct(
        private readonly RequestStack $requestStack,
        private readonly JsonLdBuilder $jsonLd,
        private readonly SiteGraphJsonLd $siteGraph,
        private readonly GameEntityJsonLd $gameEntities,
        private readonly ContentJsonLd $content,
        private readonly CanonicalHost $canonicalHost,
        private readonly OgLocale $ogLocale,
        private readonly TranslatorInterface $translator,
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
            new TwigFunction('seo_profile_page', $this->jsonLd->profilePage(...)),
            new TwigFunction('seo_article', $this->jsonLd->article(...)),
            new TwigFunction('seo_champion', $this->gameEntities->champion(...)),
            new TwigFunction('seo_item', $this->gameEntities->item(...)),
            new TwigFunction('seo_rune_path', $this->gameEntities->runePath(...)),
            new TwigFunction('seo_summoner_spell', $this->gameEntities->summonerSpell(...)),
            new TwigFunction('seo_faq', $this->content->faqPage(...)),
            new TwigFunction('seo_about_page', $this->content->aboutPage(...)),
            new TwigFunction('seo_dataset', $this->content->dataset(...)),
        ];
    }

    /** Canonical URL of the current request — path only, no query string. */
    public function canonical(): string
    {
        $request = $this->request();

        return $request === null ? '' : $this->origin($request) . $request->getPathInfo();
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

        return $this->origin($request) . '/' . ltrim($path, '/');
    }

    /** og:locale for the resolved UI locale of the current request. */
    public function currentOgLocale(): string
    {
        $locale = $this->request()?->getLocale();

        return $this->ogLocale->fromUiLocale($locale ?? '');
    }

    /**
     * "{page} — {site}" title pattern; an empty page title yields the bare site
     * name. On a `/{version}/…` render the patch is woven into the page title so
     * historical snapshots never share a title with the latest page (or each
     * other) — the differentiator that keeps them out of a duplicate cluster.
     */
    public function title(string $pageTitle): string
    {
        $pageTitle = trim($pageTitle);

        $version = $this->versionedRouteVersion();
        if ($version !== '') {
            $suffix    = $this->translator->trans('versioned_suffix', ['%version%' => $version], 'seo');
            $pageTitle = $pageTitle === '' ? $suffix : $pageTitle . ' ' . $suffix;
        }

        return $pageTitle === '' ? $this->siteName : $pageTitle . self::TITLE_SEPARATOR . $this->siteName;
    }

    /** Version carried by a versioned resource route, else '' (clean/non-resource page). */
    private function versionedRouteVersion(): string
    {
        $request = $this->request();
        if ($request === null || !str_ends_with((string) $request->attributes->get('_route'), self::VERSIONED_SUFFIX)) {
            return '';
        }

        return (string) $request->attributes->get('version', '');
    }

    /** @param array<string,mixed> $data */
    public function jsonLdScript(array $data): string
    {
        return '<script type="application/ld+json">' . $this->jsonLd->encode($data) . '</script>';
    }

    /**
     * Sitewide WebSite + Organization + WebPage graph, emitted once from the base
     * layout. $pageName is the document title of the current render.
     */
    public function globalGraph(string $pageName = ''): string
    {
        $request = $this->request();
        if ($request === null) {
            return '';
        }

        $context = new SiteGraphContext(
            root:            $this->origin($request) . '/',
            pageUrl:         $this->canonical(),
            pageName:        trim($pageName),
            siteDescription: $this->translator->trans(self::SITE_DESCRIPTION_KEY),
            logoUrl:         $this->absolute(self::ORGANIZATION_LOGO_PATH),
            locale:          $request->getLocale(),
        );

        return $this->jsonLdScript($this->siteGraph->graph($context));
    }

    /** Canonical origin of a request (mirrors folded, non-default port kept). */
    private function origin(Request $request): string
    {
        return $this->canonicalHost->origin($request->getScheme(), $request->getHost(), $request->getPort());
    }

    private function request(): ?Request
    {
        return $this->requestStack->getCurrentRequest();
    }
}
