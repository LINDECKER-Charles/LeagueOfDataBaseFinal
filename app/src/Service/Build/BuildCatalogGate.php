<?php
declare(strict_types=1);

namespace App\Service\Build;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\Picker\GameMode;
use App\Service\Picker\ItemOptionsProjector;

/**
 * Validates a submitted structure against the catalogs of the SUBMITTED
 * (version, lang) and the availability rules of the SUBMITTED game mode.
 *
 * Split I/O / pure: {@see validate} only loads the catalogs through the
 * resource managers (final, storage-bound — deliberately unmocked) and hands
 * them to {@see evaluate}, the pure core unit tests exercise with fixtures.
 * Errors are (code, params) tuples ready for the translator; params carry the
 * offending item names so mode failures read as real sentences. Data-layer
 * failures (transient upstream) bubble — the controller degrades them into a
 * "catalog unavailable" flash instead of silently accepting the structure.
 */
final class BuildCatalogGate
{
    public const ERROR_ITEM_MODE = 'build.error.steps.item_mode';

    public function __construct(
        private readonly BuildStructureValidator $validator,
        private readonly ChampionManager $championManager,
        private readonly ItemManager $itemManager,
        private readonly RuneManager $runeManager,
        private readonly ItemOptionsProjector $itemProjector,
    ) {}

    /**
     * @param array<mixed> $structure decoded structure JSON
     * @return list<array{0: string, 1: array<string, string>}> translator-ready
     *         (code, params) tuples; empty means valid on this (version, lang, mode)
     */
    public function validate(array $structure, string $version, string $lang, GameMode $mode): array
    {
        return $this->evaluate($structure, $mode, [
            'runeTrees' => $this->runeManager->getData($version, $lang),
            'championIds' => array_keys($this->championManager->getData($version, $lang)['data'] ?? []),
            'itemData' => $this->itemManager->getData($version, $lang)['data'] ?? [],
        ]);
    }

    /**
     * Pure evaluation against pre-loaded catalogs.
     *
     * @param array<mixed> $structure
     * @param array{runeTrees: array<mixed>, championIds: list<int|string>, itemData: array<int|string, mixed>} $catalogs
     * @return list<array{0: string, 1: array<string, string>}>
     */
    public function evaluate(array $structure, GameMode $mode, array $catalogs): array
    {
        $codes = $this->validator->validate(
            $structure,
            $catalogs['runeTrees'],
            // PHP recasts numeric JSON keys to int — restore the string ids.
            array_map(strval(...), $catalogs['championIds']),
            array_map(strval(...), array_keys($catalogs['itemData'])),
        );

        $errors = array_map(static fn (string $code): array => [$code, []], $codes);
        $unavailable = $this->itemProjector->unavailableOn($catalogs['itemData'], $mode, $this->stepItemIds($structure));
        if ($unavailable !== []) {
            $errors[] = [self::ERROR_ITEM_MODE, ['%items%' => implode(', ', $unavailable)]];
        }

        return $errors;
    }

    /** @param array<mixed> $structure @return list<string> */
    private function stepItemIds(array $structure): array
    {
        $ids = [];
        foreach ((array) ($structure['steps'] ?? []) as $step) {
            foreach ((array) ($step['items'] ?? []) as $id) {
                if (is_scalar($id)) {
                    $ids[] = (string) $id;
                }
            }
        }

        return $ids;
    }
}
