<?php

declare(strict_types=1);

namespace App\Service\Seo\Inventory;

use App\Service\API\AbstractManager;

/**
 * Reads a resource index for the crawler-facing surfaces (sitemap, /llms.txt,
 * /about/data).
 *
 * Both surfaces enumerate the same datasets under the same two rules, which used
 * to be restated — and could drift — in each of them: ids and counts are
 * language-invariant, so one reference dataset answers for all 21 languages; and
 * an upstream failure must degrade (a skipped section, an unknown count) instead
 * of turning a page crawlers punish for erroring into a 500.
 */
final class DatasetIndexReader
{
    /** Ids and counts are language-invariant — one dataset answers for all 21. */
    private const REFERENCE_LANG = 'en_US';

    /**
     * @return array<array-key, mixed>|null null when the dataset is unreadable
     *         (unknown ≠ empty — callers must be able to tell them apart)
     */
    public function read(AbstractManager $manager, string $version): ?array
    {
        if ($version === '') {
            return null;
        }

        try {
            return $manager->listIndex($version, self::REFERENCE_LANG);
        } catch (\Throwable) {
            return null;
        }
    }
}
