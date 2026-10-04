<?php
declare(strict_types=1);

namespace App\Service\Community;

use App\Service\Picker\GameMode;

/**
 * Immutable filter criteria of the public trends ranking. Groups the optional
 * facets (champion, game mode, authoring language) so the repository queries
 * take one cohesive argument instead of a growing positional list; a null facet
 * means "no restriction" (e.g. the default "all languages").
 */
final readonly class TrendsFilter
{
    public function __construct(
        public ?string $championId = null,
        public ?GameMode $mode = null,
        public ?string $language = null,
    ) {}
}
