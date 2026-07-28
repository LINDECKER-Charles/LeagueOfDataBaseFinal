<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * The per-request values the sitewide schema.org graph is built from. Groups
 * what would otherwise be a six-argument builder call; every field is resolved
 * once per render by the Twig layer.
 */
final readonly class SiteGraphContext
{
    public function __construct(
        /** Absolute site root, trailing slash included (identity of the WebSite node). */
        public string $root,
        /** Canonical URL of the page being rendered. */
        public string $pageUrl,
        /** Human title of the page, without the site-name suffix. */
        public string $pageName,
        /** One-line pitch of the project, in the visitor's locale. */
        public string $siteDescription,
        /** Absolute URL of the brand mark. */
        public string $logoUrl,
        /** UI locale of this render (BCP 47). */
        public string $locale,
    ) {}
}
