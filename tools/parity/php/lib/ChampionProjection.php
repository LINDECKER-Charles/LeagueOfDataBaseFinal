<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Service\API\ChampionManager;
use App\Service\API\DatasetRef;
use App\Service\Catalog\Facet\Schema\ChampionFacets;

/**
 * Champions as the legacy detail page assembles them: the summary of champion.json, merged
 * with the per-champion detail file when a detail page stored it, chroma skins removed with
 * the CommunityDragon chromas of the same page (ChampionController::champion()).
 */
final class ChampionProjection
{
    private readonly ChampionManager $manager;

    private readonly ChampionFacets $facets;

    public function __construct(
        private readonly LegacyCatalog $catalog,
        private readonly DatasetRef $ref,
    ) {
        $this->manager = $catalog->champions();
        $this->facets  = new ChampionFacets($this->manager, new NullTranslator());
    }

    /** @return list<array<string, mixed>> */
    public function entries(): array
    {
        // The resource token reads the en_US entry: it must be stored too.
        $english = $this->ref->withLang(LegacyCatalog::FALLBACK_LANG);
        $this->catalog->requireDataset($this->manager, $english);
        $data = $this->catalog->requireDataset($this->manager, $this->ref);

        $entries = [];
        foreach ($data['data'] ?? [] as $key => $entry) {
            if (is_array($entry)) {
                $entries[] = $this->champion((string) $key, $entry);
            }
        }

        return $entries;
    }

    /**
     * @param array<string, mixed> $entry
     * @return array<string, mixed>
     */
    private function champion(string $key, array $entry): array
    {
        $values   = $this->facets->valuesOf($key, $entry, $this->ref);
        $champion = [
            'id'          => (string) ($entry['id'] ?? $key),
            'key'         => (string) ($entry['key'] ?? ''),
            'name'        => (string) ($entry['name'] ?? ''),
            'title'       => (string) ($entry['title'] ?? ''),
            'resource'    => $values['resource'],
            'attackRange' => $values['range'] ?? null,
            'image'       => $this->image($entry['image']['full'] ?? null),
            'detail'      => false,
        ];

        $detail = $this->storedDetail($key, $entry);

        return $detail === null ? $champion : array_merge($champion, $this->detailed($detail));
    }

    /**
     * The summary merged with its stored detail, or null when no detail page stored both
     * the detail and the chromas it reads.
     *
     * @param array<string, mixed> $summary
     * @return array<string, mixed>|null
     */
    private function storedDetail(string $key, array $summary): ?array
    {
        [$version, $lang] = [$this->ref->version, $this->ref->lang];
        $championKey = (string) ($summary['key'] ?? '');
        $detailPath  = sprintf('data/%s/%s/championDetail/%s.json', $version, $lang, $key);
        $chromaPath  = sprintf('data/%s/cdragon/chromas/%s.json', $version, $championKey);
        // getChromas() answers [] without any read for a non-numeric key.
        $hasChromas = !ctype_digit($championKey) || $this->catalog->has($chromaPath);
        if (!$hasChromas || !$this->catalog->has($detailPath)) {
            return null;
        }

        $champion = array_merge($summary, $this->manager->getDetail($key, $version, $lang));
        $chromas  = $this->manager->getChromas($championKey, $version);
        $champion['skins']   = $this->manager->withoutChromaSkins(
            $champion['skins'] ?? [],
            $chromas,
        );
        $champion['chromas'] = $chromas;

        return $champion;
    }

    /**
     * @param array<string, mixed> $champion
     * @return array<string, mixed>
     */
    private function detailed(array $champion): array
    {
        $passive = $champion['passive'] ?? null;

        return [
            'detail'  => true,
            'passive' => is_array($passive)
                ? [
                    'name'  => (string) ($passive['name'] ?? ''),
                    'image' => $this->image($passive['image']['full'] ?? null),
                ]
                : null,
            'spells'  => array_map(
                fn (array $spell): array => [
                    'id'    => (string) ($spell['id'] ?? ''),
                    'name'  => (string) ($spell['name'] ?? ''),
                    'image' => $this->image($spell['image']['full'] ?? null),
                ],
                array_values(array_filter($champion['spells'] ?? [], 'is_array')),
            ),
            'skins'   => SkinProjection::of($champion['skins'], $champion['chromas']),
        ];
    }

    /** @return array{file: string, status: string, url: ?string}|null */
    private function image(mixed $file): ?array
    {
        return ImageStatus::of($this->manager, $this->ref->version, $file);
    }
}
