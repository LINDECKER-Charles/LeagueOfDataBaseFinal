<?php
declare(strict_types=1);

namespace App\Service\Picker;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;

/**
 * Thin façade between the DDragon managers (final, storage-bound) and the pure
 * projectors: it only fetches (data, images) per resource and delegates every
 * shape decision — which keeps the whole projection/resolution logic unit-
 * testable without object storage. Image resolution runs over the full set,
 * exactly like the render-all list pages: warm manifests make it a map lookup,
 * a cold one defers ingestion after the response (placeholders now, warm next
 * visit). Upstream failures bubble — callers own the fallback policy.
 */
final class PickerCatalog
{
    public function __construct(
        private readonly ChampionManager $champions,
        private readonly ItemManager $items,
        private readonly RuneManager $runes,
        private readonly SummonerManager $summoners,
        private readonly ChampionOptionsProjector $championProjector,
        private readonly ItemOptionsProjector $itemProjector,
        private readonly RuneOptionsProjector $runeProjector,
        private readonly SummonerOptionsProjector $summonerProjector,
    ) {}

    /** @return list<array<string, mixed>> */
    public function championOptions(string $version, string $lang): array
    {
        [$data, $images] = $this->championSet($version, $lang);

        return $this->championProjector->project($data, $images);
    }

    /** @return list<array<string, mixed>> */
    public function itemOptions(string $version, string $lang, GameMode $mode = GameMode::DEFAULT): array
    {
        [$data, $images] = $this->itemSet($version, $lang);

        return $this->itemProjector->project($data, $images, $mode);
    }

    /** @return list<array<string, mixed>> */
    public function runeTrees(string $version, string $lang): array
    {
        [$data, $images] = $this->runeSet($version, $lang);

        return $this->runeProjector->project($data, $images);
    }

    /** @return list<array<string, mixed>> */
    public function summonerOptions(string $version, string $lang): array
    {
        [$data, $images] = $this->summonerSet($version, $lang);

        return $this->summonerProjector->project($data, $images);
    }

    /** @return ?array{id: string, name: string, image: ?string, type: string} */
    public function resolveChampion(string $id, string $version, string $lang): ?array
    {
        [$data, $images] = $this->championSet($version, $lang);

        return $this->championProjector->resolve($data, $images, $id);
    }

    /** @return ?array{id: string, name: string, image: ?string, type: string} */
    public function resolveItem(string $id, string $version, string $lang): ?array
    {
        [$data, $images] = $this->itemSet($version, $lang);

        return $this->itemProjector->resolve($data, $images, $id);
    }

    /** @return ?array{id: string, name: string, image: ?string, type: string} */
    public function resolveRune(string $id, string $version, string $lang): ?array
    {
        [$data, $images] = $this->runeSet($version, $lang);

        return $this->runeProjector->resolve($data, $images, $id);
    }

    /** @return ?array{id: string, name: string, image: ?string, type: string} */
    public function resolveSummoner(string $id, string $version, string $lang): ?array
    {
        [$data, $images] = $this->summonerSet($version, $lang);

        return $this->summonerProjector->resolve($data, $images, $id);
    }

    /** @return array{0: array<string, array<string, mixed>>, 1: list<?string>} */
    private function championSet(string $version, string $lang): array
    {
        $data = $this->champions->getData($version, $lang)['data'] ?? [];

        return [$data, $this->champions->getImages($version, $lang, false, array_values($data))];
    }

    /** @return array{0: array<int|string, array<string, mixed>>, 1: array<string, ?string>} */
    private function itemSet(string $version, string $lang): array
    {
        $data = $this->items->getData($version, $lang)['data'] ?? [];

        return [$data, $this->items->getImages($version, $lang, false, array_values($data))];
    }

    /** @return array{0: list<array<string, mixed>>, 1: array<string, mixed>} */
    private function runeSet(string $version, string $lang): array
    {
        $data = array_values($this->runes->getData($version, $lang));

        return [$data, $this->runes->getImages($version, $lang, false, $data)];
    }

    /** @return array{0: array<string, array<string, mixed>>, 1: array<string, ?string>} */
    private function summonerSet(string $version, string $lang): array
    {
        $data = $this->summoners->getData($version, $lang)['data'] ?? [];

        return [$data, $this->summoners->getImages($version, $lang, false, array_values($data))];
    }
}
