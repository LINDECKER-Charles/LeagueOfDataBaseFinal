<?php
declare(strict_types=1);

namespace App\Service\Profile;

use App\Service\API\ChampionManager;

/**
 * Skin catalogue behind the profile banner favorite. Two jobs, one owner:
 *  - {@see options()} lists a champion's skins for the banner picker;
 *  - {@see resolveBanner()} turns the stored "{championId}_{skinNum}" id into
 *    ready-to-hotlink splash art for the hero.
 *
 * Skin art is served straight from the Data Dragon CDN (hotlink assumed, never
 * ingested — a deliberate TTFB choice, see project notes). The banner URLs are
 * therefore pure functions of the id: a cold data layer never blanks the hero,
 * it only costs the human-readable skin name (which degrades to the champion).
 */
final class ChampionSkins
{
    private const CENTERED_BASE = 'https://ddragon.leagueoflegends.com/cdn/img/champion/centered';
    private const SPLASH_BASE = 'https://ddragon.leagueoflegends.com/cdn/img/champion/splash';
    private const LOADING_BASE = 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading';

    /** DDragon names the base skin "default"; we surface the champion name instead. */
    private const BASE_SKIN_NAME = 'default';

    /** "{championId}_{skinNum}" — champion ids are alphanumeric, skin nums are small. */
    private const ID_PATTERN = '/^[A-Za-z0-9]+_\d{1,4}$/';

    public function __construct(private readonly ChampionManager $champions) {}

    /**
     * Skins of one champion, base first, for the banner picker. `image` is the
     * portrait-crop loading art (grid tile); `banner` is the wide centered art
     * the socket/hero preview reuses — so the client never builds a CDN URL.
     * Upstream failures bubble — the API endpoint owns the fallback policy.
     *
     * @return list<array{id: string, num: int, name: string, image: string, banner: string}>
     */
    public function options(string $championId, string $version, string $lang): array
    {
        $detail = $this->champions->getDetail($championId, $version, $lang);
        $championName = (string) ($detail['name'] ?? $championId);

        $options = [];
        foreach ($this->realSkins($detail, $version) as $skin) {
            $num = (int) ($skin['num'] ?? 0);
            $options[] = [
                'id'     => $this->composeId($championId, $num),
                'num'    => $num,
                'name'   => $this->skinName($skin['name'] ?? null, $championName),
                'image'  => $this->art(self::LOADING_BASE, $championId, $num),
                'banner' => $this->art(self::CENTERED_BASE, $championId, $num),
            ];
        }

        return $options;
    }

    /**
     * Resolve the stored banner id into hero art. Null for an empty/malformed id.
     * The name is best-effort: an unreachable data layer leaves the URLs intact
     * and falls back to the champion id as the label.
     *
     * @return ?array{id: string, championId: string, num: int, name: string, banner: string, splash: string}
     */
    public function resolveBanner(?string $skinId, string $version, string $lang): ?array
    {
        $parsed = $this->parse($skinId);
        if ($parsed === null) {
            return null;
        }
        [$championId, $num] = $parsed;

        return [
            'id'         => $skinId,
            'championId' => $championId,
            'num'        => $num,
            'name'       => $this->resolveName($championId, $num, $version, $lang),
            'banner'     => $this->art(self::CENTERED_BASE, $championId, $num),
            'splash'     => $this->art(self::SPLASH_BASE, $championId, $num),
        ];
    }

    /** Cheap, data-layer-free format gate for the save path. */
    public function isWellFormed(string $skinId): bool
    {
        return preg_match(self::ID_PATTERN, $skinId) === 1;
    }

    /**
     * Base-skin (num 0) art of a champion, derived purely from its id — used as
     * the hero background when no favorite skin is set.
     *
     * @return array{splash: string, centered: string, loading: string}
     */
    public function championArt(string $championId): array
    {
        return [
            'splash'   => $this->art(self::SPLASH_BASE, $championId, 0),
            'centered' => $this->art(self::CENTERED_BASE, $championId, 0),
            'loading'  => $this->art(self::LOADING_BASE, $championId, 0),
        ];
    }

    /**
     * The hero backdrop, in priority order: the favorite skin, else the favorite
     * champion's base splash, else nothing (caller falls back to the gradient).
     * `kind` lets the view avoid drawing the champion twice (background + orb).
     *
     * @param ?array{banner: string, splash: string} $skinBanner already-resolved {@see resolveBanner()}
     * @return ?array{image: string, fallback: string, kind: string}
     */
    public function heroBackground(?array $skinBanner, ?string $championId): ?array
    {
        if ($skinBanner !== null) {
            return ['image' => $skinBanner['banner'], 'fallback' => $skinBanner['splash'], 'kind' => 'skin'];
        }
        if ($championId !== null && $championId !== '') {
            $art = $this->championArt($championId);

            return ['image' => $art['centered'], 'fallback' => $art['splash'], 'kind' => 'champion'];
        }

        return null;
    }

    /**
     * Real skins only: Data Dragon inlines every chroma as a standalone skin
     * (no dedicated splash), so the picker must drop them exactly like the detail
     * page does — via the CommunityDragon chroma ids. If chroma data is down the
     * list is kept intact (degraded), never guessed from names.
     *
     * @param array<mixed> $detail a {@see ChampionManager::getDetail()} node
     * @return list<array<string, mixed>>
     */
    private function realSkins(array $detail, string $version): array
    {
        $skins = $detail['skins'] ?? [];
        if (!\is_array($skins) || $skins === []) {
            return [];
        }

        try {
            $chromas = $this->champions->getChromas((string) ($detail['key'] ?? ''), $version);
        } catch (\Throwable) {
            return array_values($skins); // CommunityDragon unreachable — keep all skins
        }

        return $this->champions->withoutChromaSkins(array_values($skins), $chromas);
    }

    /** @return ?array{0: string, 1: int} championId + skin number */
    private function parse(?string $skinId): ?array
    {
        if ($skinId === null || !$this->isWellFormed($skinId)) {
            return null;
        }
        $separator = strrpos($skinId, '_');

        return [substr($skinId, 0, $separator), (int) substr($skinId, $separator + 1)];
    }

    private function resolveName(string $championId, int $num, string $version, string $lang): string
    {
        try {
            foreach ($this->options($championId, $version, $lang) as $option) {
                if ($option['num'] === $num) {
                    return $option['name'];
                }
            }
        } catch (\Throwable) {
            // Data layer down — the art still hotlinks; the id is an honest label.
        }

        return $championId;
    }

    private function skinName(mixed $rawName, string $championName): string
    {
        $name = (string) ($rawName ?? '');

        return ($name === '' || $name === self::BASE_SKIN_NAME) ? $championName : $name;
    }

    private function composeId(string $championId, int $num): string
    {
        return $championId.'_'.$num;
    }

    private function art(string $base, string $championId, int $num): string
    {
        return sprintf('%s/%s_%d.jpg', $base, $championId, $num);
    }
}
