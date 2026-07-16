<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\Tools\UpstreamNotFoundException;
use League\Flysystem\UnableToReadFile;

final class ChampionManager extends AbstractManager implements CategoriesInterface
{
    protected const TYPE = 'champion';

    /** CommunityDragon game-data root (per patch) — the only source of chroma assets. */
    private const CDRAGON_BASE = 'https://raw.communitydragon.org/%s/plugins/rcp-be-lol-game-data/global/default';

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('https://ddragon.leagueoflegends.com/cdn/%s/img/champion/%s', $version, $name);
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
        $key = sprintf('data/%s/%s/championDetail/%s.json', $version, $lang, $name);

        try {
            $data = json_decode($this->ddragonStorage->read($key), true) ?? [];
        } catch (UnableToReadFile) {
            $url = sprintf(
                'https://ddragon.leagueoflegends.com/cdn/%s/data/%s/champion/%s.json',
                $version,
                $lang,
                $name
            );
            try {
                $data = json_decode($this->goFetcher->fetch($url), true) ?? [];
            } catch (UpstreamNotFoundException) {
                // No per-champion detail file on this patch → render on the summary.
                $data = [];
            }
            $this->ddragonStorage->write($key, json_encode($data));
        }

        return $data['data'][$name] ?? [];
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
                'https://ddragon.leagueoflegends.com/cdn/%s/img/passive/%s',
                $version,
                $passive
            );
        }

        foreach ($detail['spells'] ?? [] as $spell) {
            if ($full = $spell['image']['full'] ?? null) {
                $urlsByName[$full] = sprintf(
                    'https://ddragon.leagueoflegends.com/cdn/%s/img/spell/%s',
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

        $key = sprintf('data/%s/cdragon/chromas/%s.json', $version, $championKey);

        try {
            return json_decode($this->ddragonStorage->read($key), true) ?? [];
        } catch (UnableToReadFile) {
            $chromas = $this->fetchChromas($championKey, $version);
            $this->ddragonStorage->write($key, json_encode($chromas));

            return $chromas;
        }
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
                $raw = json_decode($this->goFetcher->fetch($this->cdragonChampionUrl($championKey, $patch)), true);
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

            $entries = [];
            foreach ($chromas as $c) {
                $path = $c['chromaPath'] ?? null;
                if (!is_string($path) || $path === '') {
                    continue;
                }
                $entries[] = [
                    'id'     => (int) ($c['id'] ?? 0),
                    'name'   => (string) ($c['name'] ?? ''),
                    'colors' => array_values(array_filter(
                        (array) ($c['colors'] ?? []),
                        static fn ($v): bool => is_string($v) && $v !== '',
                    )),
                    'image'  => $this->cdragonAssetUrl($path, $patch),
                ];
            }

            if ($entries !== []) {
                $out[(string) $skinId] = $entries;
            }
        }

        return $out;
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

    public function getByName(string $name, string $version, string $lang): array
    {
        $data = $this->getData($version, $lang)['data'] ?? [];
        if (isset($data[$name])) {
            return $data[$name];
        }

        throw new \RuntimeException(sprintf('Aucun champion trouvé avec l\'ID "%s".', $name));
    }

    public function searchByName(string $name, string $version, string $lang, int $max = 0): array
    {
        if (mb_strlen($name) < 2 || mb_strlen($name) > 50) {
            throw new \InvalidArgumentException('Nom invalide.');
        }

        $data = $this->getData($version, $lang)['data'] ?? [];
        if (!is_array($data)) {
            throw new \RuntimeException('Format de données invalide.');
        }

        $results = [];
        $search = mb_strtolower($name);
        foreach ($data as $champion) {
            if ($max !== 0 && count($results) >= $max) {
                break;
            }
            $idMatch = isset($champion['id']) && str_contains(mb_strtolower($champion['id']), $search);
            $nameMatch = isset($champion['name']) && str_contains(mb_strtolower($champion['name']), $search);
            if ($idMatch || $nameMatch) {
                $results[] = $champion;
            }
        }

        return $results;
    }

    protected function dataList(array $raw): array
    {
        return array_values($raw['data'] ?? []);
    }

    protected function imageEntries(array $data): array
    {
        $entries = [];
        foreach ($data as $d) {
            if (($name = $d['name'] ?? null) && ($img = $d['image']['full'] ?? null)) {
                $entries[$img] = $name;
            }
        }

        return $entries;
    }

    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if (!$data) {
            $data = $this->dataList($this->getData($version, $lang));
        }

        $resolved = $this->resolveImages($version, array_keys($this->imageEntries($data)), $force);

        $result = [];
        foreach ($data as $d) {
            if (($d['name'] ?? null) && ($img = $d['image']['full'] ?? null)) {
                $result[] = $resolved[$img] ?? null;
            }
        }

        return $result;
    }

    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''): string
    {
        return $this->resolveImage($version, $name, $force);
    }

    public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array
    {
        $json = $this->getData($version, $langue)['data'] ?? [];

        $ttSum = count($json);
        if ($nb === 0 || $nb > $ttSum) {
            $nb = $ttSum > 20 ? 20 : $ttSum;
        }
        $ttPage = (int) ceil($ttSum / max(1, $nb));
        if ($numPage > $ttPage) {
            $numPage = 1;
        }

        $json = $numPage <= 1
            ? $this->splitJson($nb, 0, $json)
            : $this->splitJson($nb, $nb * ($numPage - 1), $json);

        $images = $this->getImages($version, $langue, false, $json);

        return [
            static::TYPE.'s' => $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => static::TYPE,
            ],
        ];
    }
}
