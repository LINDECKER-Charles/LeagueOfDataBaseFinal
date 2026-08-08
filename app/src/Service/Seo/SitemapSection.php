<?php

declare(strict_types=1);

namespace App\Service\Seo;

use App\Service\API\AbstractManager;

/**
 * One crawlable resource family in the sitemap: where its list page lives, which
 * dataset enumerates its detail pages, and the prefix those detail URLs share.
 */
final readonly class SitemapSection
{
    /**
     * @param string $detailPrefix detail-URL prefix, trailing slash included (e.g. "/champion/")
     * @param string $listPath     clean list path (e.g. "/champions")
     */
    public function __construct(
        public string $detailPrefix,
        public AbstractManager $manager,
        public string $listPath,
    ) {}
}
