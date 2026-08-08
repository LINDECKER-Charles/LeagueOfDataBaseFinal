<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\Tools\UpstreamNotFoundException;

final class ChampionManager extends AbstractManager implements CategoriesInterface
{
    /** CommunityDragon game-data root (per patch) — the only source of chroma assets. */
    private const CDRAGON_BASE =
        'https://raw.communitydragon.org/%s/plugins/rcp-be-lol-game-data/global/default';

    public function type(): string
    {
        return 'champion';
    }

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('%s/%s/img/champion/%s', self::DDRAGON_CDN, $version, $name);
    }

    /**
     * Full champion detail (spells, passive, skins, lore, tips) — a heavier
     * per-champion payload than {@see getByName()}'s summary. Cached in object
     * storage under its own key, fetched once through the gateway on a miss.
     *
     * @return array<mixed> the champion node, or [] when unavailable
     */
    public function getDetail(string $name, string $version, string $lang): array
    {
        $data = $this->storedJson(
            $this->scopedDataKey($version, $lang, 'championDetail', $name.'.json'),
            fn (): array => $this->fetchDetail($name, $version, $lang),
        );

        return $data['data'][$name] ?? [];
    }

    /** @return array<mixed> */
    private function fetchDetail(string $name, string $version, string $lang): array
    {
        $url = sprintf(
            '%s/%s/data/%s/champion/%s.json',
            self::DDRAGON_CDN,
            $version,
            $lang,
            $name
        );

        try {
            return json_decode($this->goFetcher->fetch($url), true) ?? [];
        } catch (UpstreamNotFoundException) {
            // No per-champion detail file on this patch → render on the summary.
            return [];
        }
    }

    /**
     * Ingest the passive + spell icons of a detail payload (their DDragon paths
     * differ from champion portraits — {@code img/passive/…}, {@code img/spell/…}).
     *
     * @param array<mixed> $detail a {@see getDetail()} node
     * @return array<string,string> image.full => cdn path
     */
    public function getAbilityImages(array $detail, string $version): array
    {
        $urlsByName = [];

        if ($passive = $detail['passive']['image']['full'] ?? null) {
            $urlsByName[$passive] = sprintf(
                '%s/%s/img/passive/%s',
                self::DDRAGON_CDN,
                $version,
                $passive
            );
        }

        foreach ($detail['spells'] ?? [] as $spell) {
            if ($full = $spell['image']['full'] ?? null) {
                $urlsByName[$full] = sprintf(
                    '%s/%s/img/spell/%s',
                    self::DDRAGON_CDN,
                    $version,
                    $full
                );
            }
        }

        return $urlsByName === [] ? [] : $this->resolveExternalImages($version, $urlsByName);
    }

    /**
     * Chroma variants per skin, sourced from CommunityDragon — Data Dragon carries
     * only a boolean `chromas` flag, never the colours or preview art. Keyed by the
     * DDragon skin id (identical to CDragon's, e.g. "799001"), each entry is a chroma
     * with a display name, its accent colours and a ready-to-hotlink swatch URL. A
     * chroma has no dedicated splash — this preview disc is its whole art.
     *
     * Slimmed to that shape, then cached in object storage like {@see getDetail()};
     * a miss goes once through the gateway. Best-effort: returns [] when unavailable
     * so the detail page never breaks on it, and a transient failure is left to
     * bubble (never persisted as empty).
     *
     * @return array<string, list<array{id:int, name:string, colors:list<string>, image:string}>>
     */
    public function getChromas(string $championKey, string $version): array
    {
        if ($championKey === '' || !ctype_digit($championKey)) {
            return [];
        }

        return $this->storedJson(
            $this->scopedDataKey($version, 'cdragon', 'chromas', $championKey.'.json'),
            fn (): array => $this->fetchChromas($championKey, $version),
        );
    }

    /**
     * @return array<string, list<array{id:int, name:string, colors:list<string>, image:string}>>
     */
    private function fetchChromas(string $championKey, string $version): array
    {
        // CommunityDragon is versioned by major.minor. The newest DDragon patch may
        // not be cut there yet, and very old ones can be gone — fall back to `latest`
        // (the canonical, additive chroma set) so the feature never silently vanishes
        // on the most-used version.
        foreach (array_unique([$this->cdragonPatch($version), 'latest']) as $patch) {
            try {
                $raw = json_decode(
                    $this->goFetcher->fetch($this->cdragonChampionUrl($championKey, $patch)),
                    true
                );
            } catch (UpstreamNotFoundException) {
                continue; // no data file on this patch → try the fallback
            }
            if (is_array($raw)) {
                return $this->slimChromas($raw['skins'] ?? [], $patch);
            }
        }

        return [];
    }

    /**
     * @param array<mixed> $skins CommunityDragon skin nodes
     * @return array<string, list<array{id:int, name:string, colors:list<string>, image:string}>>
     */
    private function slimChromas(array $skins, string $patch): array
    {
        $out = [];
        foreach ($skins as $skin) {
            $chromas = $skin['chromas'] ?? null;
            $skinId  = $skin['id'] ?? null;
            if (!is_array($chromas) || $chromas === [] || $skinId === null) {
                continue;
            }

            $entries = array_values(array_filter(array_map(
                fn ($chroma): ?array => is_array($chroma)
                    ? $this->mapChroma($chroma, $patch)
                    : null,
                $chromas,
            )));

            if ($entries !== []) {
                $out[(string) $skinId] = $entries;
            }
        }

        return $out;
    }

    /**
     * @param array<mixed> $chroma a CommunityDragon chroma node
     * @return ?array{id:int, name:string, colors:list<string>, image:string}
     *         null when it carries no asset path
     */
    private function mapChroma(array $chroma, string $patch): ?array
    {
        $path = $chroma['chromaPath'] ?? null;
        if (!is_string($path) || $path === '') {
            return null;
        }

        return [
            'id'     => (int) ($chroma['id'] ?? 0),
            'name'   => (string) ($chroma['name'] ?? ''),
            'colors' => array_values(array_filter(
                (array) ($chroma['colors'] ?? []),
                static fn ($color): bool => is_string($color) && $color !== '',
            )),
            'image'  => $this->cdragonAssetUrl($path, $patch),
        ];
    }

    /**
     * Data Dragon inlines every chroma as a standalone skin entry (e.g.
     * "Popstar Ahri (Amethyst)") — dozens per modern champion, none with a
     * dedicated splash, so they render as broken figures. Each carries the same
     * id as the CommunityDragon chroma we already surface through the ChromaStrip,
     * so drop any skin whose id is a known chroma; their parent skins stay.
     *
     * No-op when chroma data is unavailable (returns the list untouched) rather
     * than guessing from names.
     *
     * @param list<array<string, mixed>> $skins   DDragon skin nodes
     * @param array<string, list<array{id:int, name:string, colors:list<string>, image:string}>>
     *        $chromas {@see getChromas()}
     * @return list<array<string, mixed>>
     */
    public function withoutChromaSkins(array $skins, array $chromas): array
    {
        $chromaIds = [];
        foreach ($chromas as $variants) {
            foreach ($variants as $chroma) {
                $chromaIds[(int) $chroma['id']] = true;
            }
        }

        if ($chromaIds === []) {
            return array_values($skins);
        }

        return array_values(array_filter(
            $skins,
            static fn (array $skin): bool => !isset($chromaIds[(int) ($skin['id'] ?? 0)]),
        ));
    }

    /** "15.13.1" → "15.13" (CommunityDragon patch granularity). */
    private function cdragonPatch(string $version): string
    {
        $parts = explode('.', $version);

        return isset($parts[1]) ? $parts[0].'.'.$parts[1] : $version;
    }

    private function cdragonChampionUrl(string $championKey, string $patch): string
    {
        return sprintf(self::CDRAGON_BASE.'/v1/champions/%s.json', $patch, $championKey);
    }

    /**
     * Map a CommunityDragon game-asset path to its public URL:
     * "/lol-game-data/assets/v1/champion-chroma-images/799/799002.png"
     *   → "{base}/v1/champion-chroma-images/799/799002.png" (asset paths are lowercased).
     */
    private function cdragonAssetUrl(string $gamePath, string $patch): string
    {
        $rel = ltrim(strtolower(str_replace('/lol-game-data/assets/', '', $gamePath)), '/');

        return sprintf(self::CDRAGON_BASE.'/%s', $patch, $rel);
    }

    /**
     * POSITIONAL list: one resolved path (or null) per entry carrying both a name
     * and an image node, in dataset order. Consumers realign it against the same
     * skip rule ({@see \App\Service\Picker\ChampionOptionsProjector}).
     */
    protected function projectImages(array $data, array $resolved): array
    {
        $result = [];
        foreach ($data as $entry) {
            if (($entry['name'] ?? null) && ($image = $entry['image']['full'] ?? null)) {
                $result[] = $resolved[$image] ?? null;
            }
        }

        return $result;
    }
}
