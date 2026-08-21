<?php
declare(strict_types=1);

namespace App\Service\API\Concern;

use App\Service\API\DatasetRef;

/**
 * Image resolution shared by the DDragon resource managers: which images a data
 * slice references, and the public paths they are served from. Split out of
 * {@see AbstractManager} so "turn a name into a path" stays separable from
 * "fetch it, store it and record it" ({@see IngestsImages}).
 *
 * Composed onto {@see AbstractManager}: reads its datasets, walks the collection
 * seams of {@see PaginatesResources} and consults the per-version manifest
 * ledger owned by {@see IngestsImages}. Resources diverge on three seams only:
 * the CDN url of one image file ({@see imageUrl}), which images a slice
 * references ({@see imageEntries}) and the shape the resolved paths come back in
 * ({@see projectImages}).
 */
trait ResolvesImages
{
    /** Build the DDragon image URL for a single file name (per-resource). */
    abstract protected function imageUrl(string $version, string $name): string;

    /**
     * Project a data slice onto its resolved image paths. Default shape: paths
     * keyed by entry ID — the entry's own `id`, else the slice key (items file
     * their id in the map key only). Entries without a display name or an image
     * node are absent from the map: a missing key reads as null, and no
     * consumer ever has to re-derive a positional skip rule (the pre-LoL
     * Classic positional champion list forced exactly that on its pickers).
     * Runes override with their nested tree shape.
     *
     * @param array<mixed> $data
     * @param array<string,string> $resolved image file name => cdn path
     * @return array<mixed>
     */
    protected function projectImages(array $data, array $resolved): array
    {
        $result = [];
        foreach ($data as $key => $entry) {
            $image = $entry['image']['full'] ?? null;
            if ($image === null || !($entry['name'] ?? null)) {
                continue;
            }
            $result[(string) ($entry['id'] ?? $key)] = $resolved[$image] ?? null;
        }

        return $result;
    }

    /**
     * Map every image of a data slice to its display name — the single source of
     * "which images this slice needs" (getImages, collectPlan, ingest). Default:
     * `image.full` file keyed by `name` (champion/item); summoner/runes override.
     *
     * @param array<mixed> $data
     * @return array<string,string> imageFileName => display name
     */
    protected function imageEntries(array $data): array
    {
        $entries = [];
        foreach ($data as $entry) {
            if (($name = $entry['name'] ?? null) && ($image = $entry['image']['full'] ?? null)) {
                $entries[$image] = $name;
            }
        }

        return $entries;
    }

    /**
     * Resolve every image of the resource for a dataset. The prologue (which
     * slice, which images it needs) is shared; only the returned shape is
     * per-resource ({@see projectImages}).
     *
     * The whole collection is walked with its keys (the resource id for the
     * `data`-map resources) so an id-keyed projection never has to recover them.
     *
     * @param array<mixed> $data optional pre-sliced collection (key-preserving);
     *                           empty = the whole collection
     * @return array<mixed>
     */
    final public function getImages(
        DatasetRef $dataset,
        bool $force = false,
        array $data = []
    ): array {
        $data  = $data ?: $this->paginationCollection($this->dataset($dataset));
        $names = array_keys($this->imageEntries($data));

        return $this->projectImages(
            $data,
            $this->resolveImages($dataset->version, $names, $force),
        );
    }

    /**
     * Single-image entrypoint for detail pages. Always synchronous — a detail
     * page must paint the real icon, never a placeholder it cannot replace.
     *
     * @throws \RuntimeException when the image is unavailable upstream
     */
    public function getImage(string $version, string $name): string
    {
        $resolved = $this->resolveImages($version, [$name]);
        if (!isset($resolved[$name])) {
            throw new \RuntimeException(
                sprintf('Image indisponible: %s (%s)', $name, $this->type())
            );
        }

        return $resolved[$name];
    }

    /**
     * Resolve image file names to public CDN paths for a version. Hits come from
     * the per-(version,type) manifest; misses are fetched in a single parallel
     * batch through the gateway, stored content-addressed (dedup) and recorded.
     *
     * @param string[] $names
     * @return array<string,string> name => cdn path
     */
    protected function resolveImages(string $version, array $names, bool $force = false): array
    {
        $urlsByName = [];
        foreach (array_unique($names) as $name) {
            $urlsByName[$name] = $this->imageUrl($version, $name);
        }

        [$result, $missing] = $this->partitionAgainstManifest($version, $urlsByName, $force);
        if ($missing === []) {
            return $result;
        }

        // Cold on a list/preview render (a withDeferral scope): don't block on a
        // multi-second batch fetch. Queue the ingestion for after the response is
        // sent (kernel.terminate) — placeholders now, warm on the next visit.
        // Detail/picker/search/build renders and CLI warmup ingest inline.
        if ($this->ingestion->shouldDefer()) {
            $this->ingestion->defer(fn (): array => $this->ingestMissing($version, $missing));

            return $result;
        }

        return $result + $this->ingestMissing($version, $missing);
    }

    /**
     * Run an image-resolving routine in a scope where cold misses defer to
     * kernel.terminate ({@see \App\Service\Storage\DeferredImageIngestor::withDeferral})
     * instead of blocking the response — the list/preview policy, opted into by
     * {@see PaginatesResources::sliceImages} and by the secondary-icon batches of
     * a list render ({@see ItemManager::relatedIndex}). Detail/build/picker
     * renders never call it — they resolve inline so a cold version paints real
     * icons.
     *
     * @template T
     * @param callable():T $resolve
     * @return T
     */
    protected function withImageDeferral(callable $resolve): mixed
    {
        return $this->ingestion->withDeferral($resolve);
    }

    /**
     * Ingest images from explicit DDragon URLs whose shape differs from
     * {@see imageUrl()} — champion spell/passive icons ({@code img/spell/…},
     * {@code img/passive/…}), splash art, etc. Reuses the per-type manifest,
     * blob dedup and WebP sibling; synchronous, for detail pages.
     *
     * @param array<string,string> $urlsByName name => ddragon url
     * @return array<string,string> name => cdn path
     */
    protected function resolveExternalImages(string $version, array $urlsByName): array
    {
        [$result, $missing] = $this->partitionAgainstManifest($version, $urlsByName, false);

        return $missing === [] ? $result : $result + $this->ingestMissing($version, $missing);
    }

    /**
     * Split the wanted images into those the manifest already settles and those
     * still to fetch — the one lookup shared by both resolution entrypoints.
     * A null manifest entry is a SETTLED definitive absence (the CDN 403/404s
     * this name for this version): it resolves to null without another fetch.
     *
     * @param array<string,string> $urlsByName name => ddragon url
     * @return array{0: array<string,?string>, 1: array<string,string>}
     *         name => cdn path or null, then ddragon url => name
     */
    private function partitionAgainstManifest(
        string $version,
        array $urlsByName,
        bool $force
    ): array {
        $manifest = $this->loadManifest($version);
        $result   = [];
        $missing  = [];

        foreach ($urlsByName as $name => $url) {
            if (!$force && \array_key_exists($name, $manifest)) {
                $result[$name] = $manifest[$name];
            } else {
                $missing[$url] = $name;
            }
        }

        return [$result, $missing];
    }
}
