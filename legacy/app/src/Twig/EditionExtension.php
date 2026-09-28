<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\API\Edition\ItemEditionRule;
use App\Service\API\Edition\SummonerEditionRule;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Exposes the edition rules to the list templates, which iterate the raw
 * Data Dragon maps and need to tag every card (current game vs LoL Classic)
 * without the controller re-shaping the payload. Detail pages get the edition
 * from their controller instead — they also need the counterpart, a dataset
 * lookup that does not belong in a template.
 */
final class EditionExtension extends AbstractExtension
{
    public function getFunctions(): array
    {
        return [
            new TwigFunction('item_edition', $this->itemEdition(...)),
            new TwigFunction('summoner_edition', $this->summonerEdition(...)),
        ];
    }

    public function itemEdition(string|int $id): string
    {
        return ItemEditionRule::of((string) $id)->value;
    }

    /** @param array<string, mixed> $spell a summoner.json node */
    public function summonerEdition(array $spell): string
    {
        return SummonerEditionRule::of($spell)->value;
    }
}
