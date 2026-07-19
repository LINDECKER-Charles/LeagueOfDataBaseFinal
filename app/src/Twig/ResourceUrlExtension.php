<?php

declare(strict_types=1);

namespace App\Twig;

use App\Service\Tools\ResourceUrlGenerator;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Exposes {@see ResourceUrlGenerator} to templates as `resource_path()`: the
 * version-aware replacement for `path('app_champion', {name, version, lang})`.
 */
final class ResourceUrlExtension extends AbstractExtension
{
    public function __construct(
        private readonly ResourceUrlGenerator $urls,
    ) {}

    public function getFunctions(): array
    {
        return [
            new TwigFunction('resource_path', $this->urls->resourcePath(...)),
        ];
    }
}
