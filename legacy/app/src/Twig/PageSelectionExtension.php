<?php

declare(strict_types=1);

namespace App\Twig;

use App\Service\Client\PageContextResolver;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Exposes the resolved page selection to templates as `page_selection()`
 * (`{version, lang}`), so the header and bottom-nav display the SAME patch the
 * page rendered with. The single source is {@see PageContextResolver} (path >
 * ?version= > session) — replacing the per-template re-derivation that ignored
 * the query and could drift from the actual content.
 */
final class PageSelectionExtension extends AbstractExtension
{
    public function __construct(
        private readonly PageContextResolver $pageContext,
    ) {}

    public function getFunctions(): array
    {
        return [
            new TwigFunction('page_selection', $this->pageContext->selection(...)),
        ];
    }
}
