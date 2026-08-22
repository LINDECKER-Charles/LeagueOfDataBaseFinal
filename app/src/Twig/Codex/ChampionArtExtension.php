<?php
declare(strict_types=1);

namespace App\Twig\Codex;

use App\Service\API\Champion\ChampionArt;
use App\Service\API\Champion\ChampionArtKind;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * `champion_art(id, kind, skinNum = 0)` — the templates that iterate raw
 * Data Dragon maps (champion list, home previews, skin gallery view-model) get
 * their hotlinked art from {@see ChampionArt} instead of concatenating the CDN
 * base themselves, so the internal-spelling rule lives in exactly one place.
 */
final class ChampionArtExtension extends AbstractExtension
{
    public function __construct(private readonly ChampionArt $art) {}

    public function getFunctions(): array
    {
        return [new TwigFunction('champion_art', $this->url(...))];
    }

    /** @param string $kind a {@see ChampionArtKind} value ('splash' | 'loading' | 'centered') */
    public function url(string $championId, string $kind, int $skinNum = 0): string
    {
        return $this->art->url($championId, ChampionArtKind::from($kind), $skinNum);
    }
}
