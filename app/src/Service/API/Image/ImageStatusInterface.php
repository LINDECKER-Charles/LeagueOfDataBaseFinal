<?php
declare(strict_types=1);

namespace App\Service\API\Image;

/**
 * What the in-page image refresh needs from a resource manager: a read-only
 * look at the manifest, and a way to re-queue what the initial deferred flush
 * may have dropped. Kept apart from the warm-up contract
 * ({@see \App\Service\API\WarmableManagerInterface}) — the poller must never be
 * handed an inline ingestion entrypoint.
 */
interface ImageStatusInterface
{
    /** DDragon resource type key ('champion', 'item', 'summoner', 'runesReforged'). */
    public function type(): string;

    /**
     * @param string[] $names image file names (the manifest keys)
     * @return array{images: array<string,?string>, pending: list<string>}
     *         settled name => cdn path (null = definitive absence), then the
     *         names still to fetch
     */
    public function manifestStatus(string $version, array $names): array;

    /**
     * Queue the still-missing names for ingestion after the response.
     *
     * @param string[] $names
     */
    public function warmLater(string $version, array $names): void;
}
